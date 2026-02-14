using System.Text.Json;
using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Infrastructure.Services.Backup;

public sealed class RestoreService(
    ISqliteConnectionManager connectionManager,
    ISessionService sessionService,
    IRbacService rbacService,
    IBackupService backupService,
    BackupIntegrityChecker integrityChecker,
    IAuditService auditService) : IRestoreService
{
    public Task<BackupValidationResult> ValidateRestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        return backupService.ValidateBackupAsync(backupFilePath, cancellationToken);
    }

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

        var validation = await ValidateRestoreAsync(backupFilePath, cancellationToken);
        if (!validation.Exists)
        {
            throw new InvalidOperationException("Backup validation failed: backup file does not exist.");
        }

        if (!validation.SqliteIntegrityOk)
        {
            throw new InvalidOperationException("Backup validation failed: SQLite integrity check failed.");
        }

        if (validation.HashCheckAvailable && !validation.HashMatches)
        {
            throw new InvalidOperationException("Backup validation failed: hash verification mismatch.");
        }

        var dbPath = connectionManager.DatabasePath;
        var safetyDirectory = Path.Combine(Path.GetDirectoryName(dbPath)!, "backups", "pre_restore");
        Directory.CreateDirectory(safetyDirectory);
        var safetyCopyPath = Path.Combine(safetyDirectory, $"pre_restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");

        SqliteConnection.ClearAllPools();

        if (File.Exists(dbPath))
        {
            File.Copy(dbPath, safetyCopyPath, overwrite: true);
        }

        File.Copy(backupFilePath, dbPath, overwrite: true);

        var activeDatabaseIntegrityOk = await integrityChecker.ValidateSqliteIntegrityAsync(dbPath, cancellationToken);
        if (!activeDatabaseIntegrityOk)
        {
            if (File.Exists(safetyCopyPath))
            {
                File.Copy(safetyCopyPath, dbPath, overwrite: true);
            }

            throw new InvalidOperationException("Restore failed: restored database integrity check failed.");
        }

        await auditService.LogAsync(
            AuditActionType.Restore,
            nameof(BackupHistory),
            null,
            oldValuesJson: null,
            newValuesJson: JsonSerializer.Serialize(new
            {
                BackupFile = Path.GetFileName(backupFilePath),
                validation.HashCheckAvailable,
                validation.HashMatches,
                validation.SqliteIntegrityOk,
                validation.ActualHash,
                SafetyCopy = safetyCopyPath
            }),
            context: "Database restored from backup",
            username: initiatedBy,
            cancellationToken);
    }
}
