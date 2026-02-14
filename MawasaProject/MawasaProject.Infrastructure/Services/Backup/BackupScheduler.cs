using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Services;

namespace MawasaProject.Infrastructure.Services.Backup;

public sealed class BackupScheduler(
    IBackupService backupService,
    IAppLogger<BackupScheduler> logger) : IAsyncDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _worker;

    public void Start(TimeSpan interval, string initiatedBy = "system")
    {
        if (_worker is not null)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        _worker = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await backupService.CreateAutomaticBackupAsync(initiatedBy, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.Error(exception, "Auto backup failed.");
                }
            }
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        await _cancellationTokenSource.CancelAsync();

        if (_worker is not null)
        {
            await _worker;
        }

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        _worker = null;
    }
}
