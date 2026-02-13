using System.Security.Cryptography;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Backup;

public sealed class BackupService(
    ISqliteConnectionManager connectionManager,
    IAuditService auditService,
    ISessionService sessionService) : IBackupService
{
    public async Task<BackupMetadata> CreateManualBackupAsync(string initiatedBy, CancellationToken cancellationToken = default)
    {
        var dbPath = connectionManager.DatabasePath;
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("Database file was not found.", dbPath);
        }

        var backupDirectory = Path.Combine(Path.GetDirectoryName(dbPath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        var fileName = $"mawasa_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
        var destinationPath = Path.Combine(backupDirectory, fileName);
        File.Copy(dbPath, destinationPath, overwrite: false);

        var metadata = new BackupMetadata
        {
            FileName = fileName,
            FilePath = destinationPath,
            Hash = ComputeFileHash(destinationPath),
            SizeBytes = new FileInfo(destinationPath).Length,
            CreatedBy = initiatedBy,
            CreatedAtUtc = DateTime.UtcNow,
            Version = "v1"
        };

        await SaveHistoryAsync(metadata, cancellationToken);

        var username = sessionService.CurrentSession?.Username ?? initiatedBy;
        await auditService.LogAsync(
            AuditActionType.Backup,
            nameof(BackupHistory),
            metadata.Id.ToString(),
            oldValuesJson: null,
            newValuesJson: $"{{\"FileName\":\"{metadata.FileName}\"}}",
            context: "Manual backup created",
            username,
            cancellationToken);

        return metadata;
    }

    public async Task<IReadOnlyList<BackupMetadata>> GetBackupHistoryAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = connectionManager.CurrentTransaction;
            command.CommandText = "SELECT Id, FileName, FilePath, Hash, SizeBytes, Version, CreatedBy, CreatedAtUtc FROM BackupHistory ORDER BY CreatedAtUtc DESC;";

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
                    CreatedAtUtc = DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task SaveHistoryAsync(BackupMetadata metadata, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = connectionManager.CurrentTransaction;
            command.CommandText = "INSERT INTO BackupHistory (Id, FileName, FilePath, Hash, SizeBytes, Version, CreatedBy, CreatedAtUtc, UpdatedAtUtc) VALUES ($Id, $FileName, $FilePath, $Hash, $SizeBytes, $Version, $CreatedBy, $CreatedAtUtc, $UpdatedAtUtc);";
            command.Parameters.AddWithValue("$Id", metadata.Id.ToString());
            command.Parameters.AddWithValue("$FileName", metadata.FileName);
            command.Parameters.AddWithValue("$FilePath", metadata.FilePath);
            command.Parameters.AddWithValue("$Hash", metadata.Hash);
            command.Parameters.AddWithValue("$SizeBytes", metadata.SizeBytes);
            command.Parameters.AddWithValue("$Version", metadata.Version);
            command.Parameters.AddWithValue("$CreatedBy", metadata.CreatedBy);
            command.Parameters.AddWithValue("$CreatedAtUtc", metadata.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
