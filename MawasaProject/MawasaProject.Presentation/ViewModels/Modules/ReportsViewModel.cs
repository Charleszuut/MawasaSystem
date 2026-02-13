using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class ReportsViewModel(
    IReportService reportService,
    IDialogService dialogService) : BaseViewModel
{
    private DateTime _startDateUtc = DateTime.UtcNow.AddMonths(-1);
    private DateTime _endDateUtc = DateTime.UtcNow;
    private string _lastExportPath = string.Empty;

    public ReportsViewModel() : this(
        App.Services.GetRequiredService<IReportService>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

    public DateTime StartDateUtc
    {
        get => _startDateUtc;
        set => SetProperty(ref _startDateUtc, value);
    }

    public DateTime EndDateUtc
    {
        get => _endDateUtc;
        set => SetProperty(ref _endDateUtc, value);
    }

    public string LastExportPath
    {
        get => _lastExportPath;
        set => SetProperty(ref _lastExportPath, value);
    }

    public AsyncCommand ExportRevenueCsvCommand => new(async () => await ExportAsync("revenue"));
    public AsyncCommand ExportOverdueCsvCommand => new(async () => await ExportAsync("overdue"));
    public AsyncCommand ExportPaymentsCsvCommand => new(async () => await ExportAsync("payments"));

    private async Task ExportAsync(string mode)
    {
        await RunBusyAsync(async () =>
        {
            var filter = new ReportFilterDto
            {
                StartDateUtc = StartDateUtc,
                EndDateUtc = EndDateUtc
            };

            var csv = mode switch
            {
                "revenue" => await reportService.GenerateRevenueReportCsvAsync(filter),
                "overdue" => await reportService.GenerateOverdueReportCsvAsync(filter),
                _ => await reportService.GeneratePaymentHistoryCsvAsync(filter)
            };

            var folder = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{mode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            await File.WriteAllTextAsync(path, csv);

            LastExportPath = path;
            await dialogService.AlertAsync("Reports", $"CSV exported to {path}");
        });
    }
}
