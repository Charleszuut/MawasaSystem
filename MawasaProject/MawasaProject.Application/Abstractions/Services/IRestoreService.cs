namespace MawasaProject.Application.Abstractions.Services;

public interface IRestoreService
{
    Task<MawasaProject.Domain.Entities.BackupValidationResult> ValidateRestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupFilePath, string initiatedBy, bool confirmed, CancellationToken cancellationToken = default);
}
