using System.Text.Json;
using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Backup;

public sealed class BackupService(
    ISqliteConnectionManager connectionManager,
    IAuditService auditService,
    ISessionService sessionService,
    BackupIntegrityChecker integrityChecker) : IBackupService
{
    public Task<BackupMetadata> CreateManualBackupAsync(string initiatedBy, CancellationToken cancellationToken = default)
    {
        return CreateBackupInternalAsync(initiatedBy, isAutomatic: false, cancellationToken);
    }

    public Task<BackupMetadata> CreateAutomaticBackupAsync(string initiatedBy, CancellationToken cancellationToken = default)
    {
        return CreateBackupInternalAsync(initiatedBy, isAutomatic: true, cancellationToken);
    }

    public async Task<IReadOnlyList<BackupMetadata>> GetBackupHistoryAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = connectionManager.CurrentTransaction;
            command.CommandText = """
                SELECT Id, FileName, FilePath, Hash, SizeBytes, Version, CreatedBy, CreatedAtUtc,
                       IsAutomatic, IsEncrypted, IntegrityVerifiedAtUtc, Notes
                FROM BackupHistory
                ORDER BY CreatedAtUtc DESC;
                """;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<BackupMetadata>();

            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(new BackupMetadata
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    FileName = reader.GetString(1),
                    FilePath = reader.GetString(2),
                    Hash = reader.GetString(3),
                    SizeBytes = reader.GetInt64(4),
                    Version = reader.GetString(5),
                    CreatedBy = reader.GetString(6),
                    CreatedAtUtc = DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    IsAutomatic = reader.GetInt32(8) == 1,
                    IsEncrypted = reader.GetInt32(9) == 1,
                    IntegrityVerifiedAtUtc = reader.IsDBNull(10)
                        ? null
                        : DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    Notes = reader.IsDBNull(11) ? null : reader.GetString(11)
                });
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<BackupValidationResult> ValidateBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
        {
            return new BackupValidationResult
            {
                BackupFilePath = backupFilePath,
                Exists = false,
                HashCheckAvailable = false,
                HashMatches = false,
                SqliteIntegrityOk = false,
                Message = "Backup file does not exist."
            };
        }

        string actualHash;
        string? expectedHash;
        bool sqliteIntegrityOk;
        try
        {
            actualHash = integrityChecker.ComputeFileHash(backupFilePath);
            expectedHash = await GetExpectedHashAsync(backupFilePath, cancellationToken) ?? await ReadManifestHashAsync(backupFilePath, cancellationToken);
            sqliteIntegrityOk = await integrityChecker.ValidateSqliteIntegrityAsync(backupFilePath, cancellationToken);
        }
        catch (Exception exception)
        {
            return new BackupValidationResult
            {
                BackupFilePath = backupFilePath,
                Exists = true,
                HashCheckAvailable = false,
                HashMatches = false,
                SqliteIntegrityOk = false,
                Message = "Backup validation failed: " + exception.Message
            };
        }

        var hashCheckAvailable = !string.IsNullOrWhiteSpace(expectedHash);
        var hashMatches = !hashCheckAvailable || string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        var message = sqliteIntegrityOk && hashMatches
            ? "Backup is valid."
            : sqliteIntegrityOk
                ? "Backup content is valid but hash check failed."
                : "Backup failed SQLite integrity validation.";

        return new BackupValidationResult
        {
            BackupFilePath = backupFilePath,
            Exists = true,
            HashCheckAvailable = hashCheckAvailable,
            HashMatches = hashMatches,
            SqliteIntegrityOk = sqliteIntegrityOk,
            ExpectedHash = expectedHash,
            ActualHash = actualHash,
            Message = message
        };
    }

    private async Task<BackupMetadata> CreateBackupInternalAsync(string initiatedBy, bool isAutomatic, CancellationToken cancellationToken)
    {
        var dbPath = connectionManager.DatabasePath;
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("Database file was not found.", dbPath);
        }

        var actor = string.IsNullOrWhiteSpace(initiatedBy)
            ? sessionService.CurrentSession?.Username ?? "system"
            : initiatedBy.Trim();

        var backupDirectory = Path.Combine(Path.GetDirectoryName(dbPath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        var mode = isAutomatic ? "auto" : "manual";
        var fileName = $"mawasa_{mode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
        var destinationPath = Path.Combine(backupDirectory, fileName);
        await CreateConsistentSnapshotAsync(dbPath, destinationPath, cancellationToken);

        var hash = integrityChecker.ComputeFileHash(destinationPath);
        var integrityOk = await integrityChecker.ValidateSqliteIntegrityAsync(destinationPath, cancellationToken);
        if (!integrityOk)
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException("Backup integrity validation failed after copy.");
        }

        var metadata = new BackupMetadata
        {
            FileName = fileName,
            FilePath = destinationPath,
            Hash = hash,
            SizeBytes = new FileInfo(destinationPath).Length,
            CreatedBy = actor,
            CreatedAtUtc = DateTime.UtcNow,
            Version = "v2",
            IsAutomatic = isAutomatic,
            IsEncrypted = false,
            IntegrityVerifiedAtUtc = DateTime.UtcNow,
            Notes = isAutomatic ? "Scheduled backup" : "Manual backup"
        };

        await SaveHistoryAsync(metadata, cancellationToken);
        await WriteManifestAsync(metadata, cancellationToken);

        var username = sessionService.CurrentSession?.Username ?? actor;
        await auditService.LogAsync(
            AuditActionType.Backup,
            nameof(BackupHistory),
            metadata.Id.ToString(),
            oldValuesJson: null,
            newValuesJson: JsonSerializer.Serialize(new
            {
                metadata.FileName,
                metadata.Hash,
                metadata.SizeBytes,
                metadata.Version,
                metadata.IsAutomatic
            }),
            context: "Backup created",
            username,
            cancellationToken);

        return metadata;
    }

    private static async Task CreateConsistentSnapshotAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath))
        {
            throw new InvalidOperationException("Backup destination path already exists.");
        }

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        await using var source = new SqliteConnection(sourceBuilder.ConnectionString);
        await using var destination = new SqliteConnection(destinationBuilder.ConnectionString);
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private async Task SaveHistoryAsync(BackupMetadata metadata, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = connectionManager.CurrentTransaction;
            command.CommandText = """
                INSERT INTO BackupHistory
                    (Id, FileName, FilePath, Hash, SizeBytes, Version, CreatedBy, CreatedAtUtc, UpdatedAtUtc, IsAutomatic, IsEncrypted, IntegrityVerifiedAtUtc, Notes)
                VALUES
                    ($Id, $FileName, $FilePath, $Hash, $SizeBytes, $Version, $CreatedBy, $CreatedAtUtc, $UpdatedAtUtc, $IsAutomatic, $IsEncrypted, $IntegrityVerifiedAtUtc, $Notes);
                """;
            command.Parameters.AddWithValue("$Id", metadata.Id.ToString());
            command.Parameters.AddWithValue("$FileName", metadata.FileName);
            command.Parameters.AddWithValue("$FilePath", metadata.FilePath);
            command.Parameters.AddWithValue("$Hash", metadata.Hash);
            command.Parameters.AddWithValue("$SizeBytes", metadata.SizeBytes);
            command.Parameters.AddWithValue("$Version", metadata.Version);
            command.Parameters.AddWithValue("$CreatedBy", metadata.CreatedBy);
            command.Parameters.AddWithValue("$CreatedAtUtc", metadata.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DBNull.Value);
            command.Parameters.AddWithValue("$IsAutomatic", metadata.IsAutomatic ? 1 : 0);
            command.Parameters.AddWithValue("$IsEncrypted", metadata.IsEncrypted ? 1 : 0);
            command.Parameters.AddWithValue("$IntegrityVerifiedAtUtc", (object?)metadata.IntegrityVerifiedAtUtc?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$Notes", (object?)metadata.Notes ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task<string?> GetExpectedHashAsync(string backupFilePath, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = connectionManager.CurrentTransaction;
            command.CommandText = """
                SELECT Hash
                FROM BackupHistory
                WHERE FilePath = $FilePath OR FileName = $FileName
                ORDER BY CreatedAtUtc DESC
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$FilePath", backupFilePath);
            command.Parameters.AddWithValue("$FileName", Path.GetFileName(backupFilePath));
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result as string;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task WriteManifestAsync(BackupMetadata metadata, CancellationToken cancellationToken)
    {
        var manifest = new BackupManifest
        {
            FileName = metadata.FileName,
            Hash = metadata.Hash,
            SizeBytes = metadata.SizeBytes,
            Version = metadata.Version,
            CreatedBy = metadata.CreatedBy,
            CreatedAtUtc = metadata.CreatedAtUtc,
            IsAutomatic = metadata.IsAutomatic,
            IsEncrypted = metadata.IsEncrypted
        };

        var manifestPath = metadata.FilePath + ".meta.json";
        var json = JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    private static async Task<string?> ReadManifestHashAsync(string backupFilePath, CancellationToken cancellationToken)
    {
        var manifestPath = backupFilePath + ".meta.json";
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        return manifest?.Hash;
    }

    private sealed class BackupManifest
    {
        public string FileName { get; init; } = string.Empty;
        public string Hash { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public string Version { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
        public bool IsAutomatic { get; init; }
        public bool IsEncrypted { get; init; }
    }
}
