using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.Validation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class ReportsViewModel : BaseViewModel
{
    private readonly IReportService _reportService;
    private readonly IReportFileWriter _reportFileWriter;
    private readonly IDialogService _dialogService;

    private readonly AsyncCommand _exportRevenueCsvCommand;
    private readonly AsyncCommand _exportOverdueCsvCommand;
    private readonly AsyncCommand _exportPaymentsCsvCommand;

    private DateTime _startDateUtc = DateTime.UtcNow.AddMonths(-1);
    private DateTime _endDateUtc = DateTime.UtcNow;
    private string _customerIdText = string.Empty;
    private bool _includeOverdueOnly;
    private string _lastExportPath = string.Empty;
    private string _lastExportType = string.Empty;
    private int _lastExportRowCount;
    private DateTime? _lastExportedAtUtc;

    public ReportsViewModel()
        : this(
            App.Services.GetRequiredService<IReportService>(),
            App.Services.GetRequiredService<IReportFileWriter>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public ReportsViewModel(
        IReportService reportService,
        IReportFileWriter reportFileWriter,
        IDialogService dialogService)
    {
        _reportService = reportService;
        _reportFileWriter = reportFileWriter;
        _dialogService = dialogService;

        _exportRevenueCsvCommand = new AsyncCommand(async () => await ExportAsync("revenue"));
        _exportOverdueCsvCommand = new AsyncCommand(async () => await ExportAsync("overdue"));
        _exportPaymentsCsvCommand = new AsyncCommand(async () => await ExportAsync("payments"));
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

    public string CustomerIdText
    {
        get => _customerIdText;
        set => SetProperty(ref _customerIdText, value);
    }

    public bool IncludeOverdueOnly
    {
        get => _includeOverdueOnly;
        set => SetProperty(ref _includeOverdueOnly, value);
    }

    public string LastExportPath
    {
        get => _lastExportPath;
        set => SetProperty(ref _lastExportPath, value);
    }

    public string LastExportType
    {
        get => _lastExportType;
        set => SetProperty(ref _lastExportType, value);
    }

    public int LastExportRowCount
    {
        get => _lastExportRowCount;
        set => SetProperty(ref _lastExportRowCount, value);
    }

    public DateTime? LastExportedAtUtc
    {
        get => _lastExportedAtUtc;
        set => SetProperty(ref _lastExportedAtUtc, value);
    }

    public AsyncCommand ExportRevenueCsvCommand => _exportRevenueCsvCommand;
    public AsyncCommand ExportOverdueCsvCommand => _exportOverdueCsvCommand;
    public AsyncCommand ExportPaymentsCsvCommand => _exportPaymentsCsvCommand;

    private async Task ExportAsync(string mode)
    {
        await RunBusyAsync(async () =>
        {
            var filter = BuildFilter();
            if (filter is null)
            {
                await _dialogService.AlertAsync("Validation", string.Join("\n", ValidationErrors.Select(static x => x.Message)));
                return;
            }

            StatusMessage = $"Generating {mode} report...";
            var csv = mode switch
            {
                "revenue" => await _reportService.GenerateRevenueReportCsvAsync(filter),
                "overdue" => await _reportService.GenerateOverdueReportCsvAsync(filter),
                _ => await _reportService.GeneratePaymentHistoryCsvAsync(filter)
            };

            var path = await _reportFileWriter.WriteCsvAsync(mode, csv);

            LastExportPath = path;
            LastExportType = mode;
            LastExportedAtUtc = DateTime.UtcNow;
            LastExportRowCount = CountRows(csv);
            StatusMessage = "Report exported.";

            await _dialogService.AlertAsync("Reports", $"CSV exported to {path}");
        });
    }

    private ReportFilterDto? BuildFilter()
    {
        var errors = new List<ValidationError>();

        if (StartDateUtc > EndDateUtc)
        {
            errors.Add(new ValidationError
            {
                PropertyName = nameof(StartDateUtc),
                Message = "Start date must be earlier than end date."
            });
        }

        Guid? customerId = null;
        if (!string.IsNullOrWhiteSpace(CustomerIdText))
        {
            if (!Guid.TryParse(CustomerIdText.Trim(), out var parsed))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = nameof(CustomerIdText),
                    Message = "Customer ID must be a valid GUID."
                });
            }
            else
            {
                customerId = parsed;
            }
        }

        SetValidationErrors(errors);
        if (errors.Count > 0)
        {
            return null;
        }

        return new ReportFilterDto
        {
            StartDateUtc = StartDateUtc,
            EndDateUtc = EndDateUtc,
            CustomerId = customerId,
            IncludeOverdueOnly = IncludeOverdueOnly
        };
    }

    private static int CountRows(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return 0;
        }

        var rowCount = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length;
        return rowCount > 0 ? rowCount - 1 : 0;
    }
}
