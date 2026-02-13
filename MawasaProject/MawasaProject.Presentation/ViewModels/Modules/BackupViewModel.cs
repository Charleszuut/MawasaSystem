using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class BackupViewModel(
    IBackupService backupService,
    IRestoreService restoreService,
    IDialogService dialogService,
    AppStateStore stateStore) : BaseViewModel
{
    private string _restorePath = string.Empty;

    public BackupViewModel() : this(
        App.Services.GetRequiredService<IBackupService>(),
        App.Services.GetRequiredService<IRestoreService>(),
        App.Services.GetRequiredService<IDialogService>(),
        App.Services.GetRequiredService<AppStateStore>())
    {
    }

    public ObservableCollection<BackupMetadata> History { get; } = [];

    public string RestorePath
    {
        get => _restorePath;
        set => SetProperty(ref _restorePath, value);
    }

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var items = await backupService.GetBackupHistoryAsync();
        History.Clear();
        foreach (var item in items)
        {
            History.Add(item);
        }
    }));

    public AsyncCommand CreateBackupCommand => new(async () => await RunBusyAsync(async () =>
    {
        var user = stateStore.Session?.Username ?? "system";
        var backup = await backupService.CreateManualBackupAsync(user);
        History.Insert(0, backup);
        await dialogService.AlertAsync("Backup", $"Backup created: {backup.FileName}");
    }));

    public AsyncCommand RestoreCommand => new(async () => await RunBusyAsync(async () =>
    {
        var confirmed = await dialogService.ConfirmAsync("Restore", "Restore will overwrite current data. Continue?");
        if (!confirmed)
        {
            return;
        }

        var user = stateStore.Session?.Username ?? "system";
        await restoreService.RestoreAsync(RestorePath, user, confirmed: true);
        await dialogService.AlertAsync("Restore", "Restore completed.");
    }));
}
