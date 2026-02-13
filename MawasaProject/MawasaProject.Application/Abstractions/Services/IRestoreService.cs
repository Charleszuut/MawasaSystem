namespace MawasaProject.Application.Abstractions.Services;

public interface IRestoreService
{
    Task RestoreAsync(string backupFilePath, string initiatedBy, bool confirmed, CancellationToken cancellationToken = default);
}
