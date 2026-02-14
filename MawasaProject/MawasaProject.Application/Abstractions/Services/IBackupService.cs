using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Services;

public interface IBackupService
{
    Task<BackupMetadata> CreateManualBackupAsync(string initiatedBy, CancellationToken cancellationToken = default);
    Task<BackupMetadata> CreateAutomaticBackupAsync(string initiatedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupMetadata>> GetBackupHistoryAsync(CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
}
