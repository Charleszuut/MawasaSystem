using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class PrinterViewModel(
    IPrinterService printerService,
    IDialogService dialogService) : BaseViewModel
{
    private string _selectedPrinter = string.Empty;

    public PrinterViewModel() : this(
        App.Services.GetRequiredService<IPrinterService>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

    public ObservableCollection<string> Printers { get; } = [];

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var printers = await printerService.GetInstalledPrintersAsync();
        Printers.Clear();
        foreach (var printer in printers)
        {
            Printers.Add(printer);
        }

        SelectedPrinter = Printers.FirstOrDefault() ?? string.Empty;
    }));

    public AsyncCommand TestPrintCommand => new(async () => await RunBusyAsync(async () =>
    {
        var jobId = await printerService.EnqueueAsync(new PrintRequest
        {
            TemplateName = "Test",
            Content = "Mawasa Printer test page",
            PrinterName = SelectedPrinter,
            RetryCount = 1
        });

        await dialogService.AlertAsync("Printer", $"Test print queued: {jobId}");
    }));
}
