using System.Security.Cryptography;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Infrastructure.Services.Backup;

public sealed class RestoreService(
    ISqliteConnectionManager connectionManager,
    ISessionService sessionService,
    IRbacService rbacService,
    IAuditService auditService) : IRestoreService
{
    public async Task RestoreAsync(string backupFilePath, string initiatedBy, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new InvalidOperationException("Restore requires explicit confirmation.");
        }

        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Backup file does not exist.", backupFilePath);
        }

        var session = sessionService.CurrentSession;
        var isAdmin = rbacService.HasRole(session, UserRole.Admin);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only Admin can restore backups.");
        }

        await ValidateBackupIntegrityAsync(backupFilePath, cancellationToken);

        var dbPath = connectionManager.DatabasePath;
        var safetyCopy = dbPath + ".pre_restore";
        if (File.Exists(dbPath))
        {
            File.Copy(dbPath, safetyCopy, overwrite: true);
        }

        File.Copy(backupFilePath, dbPath, overwrite: true);

        await auditService.LogAsync(
            AuditActionType.Restore,
            "BackupHistory",
            null,
            oldValuesJson: null,
            newValuesJson: $"{{\"BackupFile\":\"{Path.GetFileName(backupFilePath)}\"}}",
            context: "Database restored from backup",
            username: initiatedBy,
            cancellationToken);
    }

    private async Task ValidateBackupIntegrityAsync(string backupFilePath, CancellationToken cancellationToken)
    {
        var candidateHash = ComputeFileHash(backupFilePath);

        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Hash FROM BackupHistory WHERE FilePath = $FilePath ORDER BY CreatedAtUtc DESC LIMIT 1;";
            command.Parameters.AddWithValue("$FilePath", backupFilePath);
            var expected = await command.ExecuteScalarAsync(cancellationToken) as string;

            if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, candidateHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backup file integrity check failed.");
            }
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
