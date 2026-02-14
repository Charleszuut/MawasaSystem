using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class BackupViewModel : BaseViewModel
{
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly IDialogService _dialogService;
    private readonly AppStateStore _stateStore;

    private string _restorePath = string.Empty;
    private BackupMetadata? _selectedBackup;
    private string _validationSummary = string.Empty;

    public BackupViewModel()
        : this(
            App.Services.GetRequiredService<IBackupService>(),
            App.Services.GetRequiredService<IRestoreService>(),
            App.Services.GetRequiredService<IDialogService>(),
            App.Services.GetRequiredService<AppStateStore>())
    {
    }

    public BackupViewModel(
        IBackupService backupService,
        IRestoreService restoreService,
        IDialogService dialogService,
        AppStateStore stateStore)
    {
        _backupService = backupService;
        _restoreService = restoreService;
        _dialogService = dialogService;
        _stateStore = stateStore;

        _stateStore.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(AppStateStore.Session), StringComparison.Ordinal))
            {
                RaisePropertyChanged(nameof(CanRestore));
            }
        };
    }

    public ObservableCollection<BackupMetadata> History { get; } = [];

    public string RestorePath
    {
        get => _restorePath;
        set => SetProperty(ref _restorePath, value);
    }

    public BackupMetadata? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (SetProperty(ref _selectedBackup, value) && value is not null)
            {
                RestorePath = value.FilePath;
            }
        }
    }

    public string ValidationSummary
    {
        get => _validationSummary;
        set => SetProperty(ref _validationSummary, value);
    }

    public bool CanRestore => _stateStore.Session?.Roles.Contains(UserRole.Admin) == true;

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var items = await _backupService.GetBackupHistoryAsync();
        History.Clear();
        foreach (var item in items)
        {
            History.Add(item);
        }

        StatusMessage = $"Loaded {History.Count} backup entries.";
    }));

    public AsyncCommand CreateBackupCommand => new(async () => await RunBusyAsync(async () =>
    {
        var user = _stateStore.Session?.Username ?? "system";
        var backup = await _backupService.CreateManualBackupAsync(user);
        History.Insert(0, backup);
        ValidationSummary = "Manual backup created and verified.";
        await _dialogService.AlertAsync("Backup", $"Backup created: {backup.FileName}");
    }));

    public AsyncCommand ValidateBackupCommand => new(async () => await RunBusyAsync(async () =>
    {
        var target = ResolveTargetBackupPath();
        if (string.IsNullOrWhiteSpace(target))
        {
            await _dialogService.AlertAsync("Validation", "Select a backup or provide a backup path.");
            return;
        }

        var result = await _backupService.ValidateBackupAsync(target);
        ValidationSummary = BuildValidationSummary(result);
        await _dialogService.AlertAsync("Backup Validation", ValidationSummary);
    }));

    public AsyncCommand RestoreCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (!CanRestore)
        {
            await _dialogService.AlertAsync("Restore", "Only Admin can restore backups.");
            return;
        }

        var target = ResolveTargetBackupPath();
        if (string.IsNullOrWhiteSpace(target))
        {
            await _dialogService.AlertAsync("Restore", "Select a backup or provide a backup path.");
            return;
        }

        var validation = await _restoreService.ValidateRestoreAsync(target);
        ValidationSummary = BuildValidationSummary(validation);
        if (!validation.Exists || !validation.SqliteIntegrityOk || (validation.HashCheckAvailable && !validation.HashMatches))
        {
            await _dialogService.AlertAsync("Restore", "Backup validation failed. Restore aborted.");
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync("Restore", "Restore will overwrite current data. Continue?");
        if (!confirmed)
        {
            return;
        }

        var user = _stateStore.Session?.Username ?? "system";
        await _restoreService.RestoreAsync(target, user, confirmed: true);
        await _dialogService.AlertAsync("Restore", "Restore completed.");
        await RefreshCommand.ExecuteAsync();
    }));

    private string ResolveTargetBackupPath()
    {
        if (!string.IsNullOrWhiteSpace(RestorePath))
        {
            return RestorePath.Trim();
        }

        return SelectedBackup?.FilePath ?? string.Empty;
    }

    private static string BuildValidationSummary(BackupValidationResult result)
    {
        if (!result.Exists)
        {
            return "Backup file not found.";
        }

        var hashPart = result.HashCheckAvailable
            ? result.HashMatches ? "Hash: OK" : "Hash: FAILED"
            : "Hash: not available";
        var sqlitePart = result.SqliteIntegrityOk ? "SQLite: OK" : "SQLite: FAILED";
        return $"{result.Message}\n{hashPart}\n{sqlitePart}\nFile: {result.BackupFilePath}";
    }
}
