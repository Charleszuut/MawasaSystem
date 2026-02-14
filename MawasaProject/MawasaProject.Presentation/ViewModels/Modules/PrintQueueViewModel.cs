using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class PrintQueueViewModel : BaseViewModel
{
    private readonly IPrinterService _printerService;
    private readonly IDialogService _dialogService;
    private PrintJob? _selectedJob;

    public PrintQueueViewModel()
        : this(
            App.Services.GetRequiredService<IPrinterService>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public PrintQueueViewModel(IPrinterService printerService, IDialogService dialogService)
    {
        _printerService = printerService;
        _dialogService = dialogService;
    }

    public ObservableCollection<PrintJob> Jobs { get; } = [];
    public ObservableCollection<PrintLog> Logs { get; } = [];

    public PrintJob? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (!SetProperty(ref _selectedJob, value))
            {
                return;
            }

            _ = LoadLogsForSelectedAsync();
        }
    }

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var jobs = await _printerService.GetQueueAsync();
        Jobs.Clear();
        foreach (var job in jobs)
        {
            Jobs.Add(job);
        }

        if (SelectedJob is null)
        {
            Logs.Clear();
            StatusMessage = $"Loaded {Jobs.Count} print job(s).";
            return;
        }

        await LoadLogsForSelectedAsync();
    }));

    public AsyncCommand ProcessQueueCommand => new(async () => await RunBusyAsync(async () =>
    {
        await _printerService.ProcessQueueAsync();
        await RefreshCommand.ExecuteAsync();
        await _dialogService.AlertAsync("Print Queue", "Queue processed.");
    }));

    public AsyncCommand RetrySelectedCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (SelectedJob is null)
        {
            await _dialogService.AlertAsync("Print Queue", "Select a job to retry.");
            return;
        }

        await _printerService.RetryAsync(SelectedJob.Id);
        await RefreshCommand.ExecuteAsync();
        await _dialogService.AlertAsync("Print Queue", $"Retry triggered for {SelectedJob.Id}.");
    }));

    private async Task LoadLogsForSelectedAsync()
    {
        if (SelectedJob is null)
        {
            Logs.Clear();
            return;
        }

        var logs = await _printerService.GetLogsAsync(SelectedJob.Id);
        Logs.Clear();
        foreach (var log in logs)
        {
            Logs.Add(log);
        }

        StatusMessage = $"Loaded {Logs.Count} log entry(s) for selected print job.";
    }
}
