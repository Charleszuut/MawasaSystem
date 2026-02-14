using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed partial class ReportsViewModel : BaseViewModel
{
    private enum ReportMode
    {
        CustomerPayments = 0,
        Issues = 1,
        Printables = 2
    }

    private readonly IReportService _reportService;
    private readonly IReportFileWriter _reportFileWriter;
    private readonly IBillingService _billingService;
    private readonly IPaymentService _paymentService;
    private readonly ICustomerService _customerService;
    private readonly IAuditService _auditService;
    private readonly IDialogService _dialogService;

    private readonly List<ReportRevenueRowItem> _allRevenueRows = [];
    private readonly List<ReportIssueTimelineRowItem> _allIssueTimelineRows = [];
    private readonly List<ReportPrintableMetricRowItem> _allPrintableRows = [];

    private readonly AsyncCommand _initializeCommand;
    private readonly AsyncCommand _applyFiltersCommand;
    private readonly RelayCommand _resetFiltersCommand;
    private readonly RelayCommand _setCustomerPaymentModeCommand;
    private readonly RelayCommand _setIssueModeCommand;
    private readonly RelayCommand _setPrintModeCommand;
    private readonly RelayCommand _setLast30DaysCommand;
    private readonly RelayCommand _setLast90DaysCommand;
    private readonly RelayCommand _setLast12MonthsCommand;
    private readonly AsyncCommand _exportCurrentReportCommand;
    private readonly AsyncCommand _printCurrentReportCommand;
    private readonly AsyncCommand _exportRevenueCsvCommand;
    private readonly AsyncCommand _exportOverdueCsvCommand;
    private readonly AsyncCommand _exportPaymentsCsvCommand;

    private ReportMode _selectedMode = ReportMode.CustomerPayments;
    private DateTime _startDateUtc = new(DateTime.UtcNow.Year, 1, 1);
    private DateTime _endDateUtc = DateTime.UtcNow;
    private string _customerFilterText = string.Empty;
    private string _groupBySelection = "Monthly";
    private string _searchText = string.Empty;
    private string _selectedSortColumn = "Period";
    private string _selectedSortDirection = "Ascending";

    private decimal _collectedAmount;
    private decimal _totalBilledAmount;
    private decimal _varianceAmount;

    private int _totalIssues;
    private int _priorityIssues;
    private int _completedIssues;
    private string _issueStatusSummary = "No issue data for the selected range.";
    private string _issueCategorySummary = "No categorized issues found.";

    private string _lastExportPath = string.Empty;
    private string _lastExportType = string.Empty;
    private int _lastExportRowCount;
    private DateTime? _lastExportedAtUtc;

    private bool _isInitialized;
    private bool _suppressSearchSortRefresh;

    public ReportsViewModel()
        : this(
            App.Services.GetRequiredService<IReportService>(),
            App.Services.GetRequiredService<IReportFileWriter>(),
            App.Services.GetRequiredService<IBillingService>(),
            App.Services.GetRequiredService<IPaymentService>(),
            App.Services.GetRequiredService<ICustomerService>(),
            App.Services.GetRequiredService<IAuditService>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public ReportsViewModel(
        IReportService reportService,
        IReportFileWriter reportFileWriter,
        IBillingService billingService,
        IPaymentService paymentService,
        ICustomerService customerService,
        IAuditService auditService,
        IDialogService dialogService)
    {
        _reportService = reportService;
        _reportFileWriter = reportFileWriter;
        _billingService = billingService;
        _paymentService = paymentService;
        _customerService = customerService;
        _auditService = auditService;
        _dialogService = dialogService;

        RevenueBreakdownRows = [];
        IssueTimelineRows = [];
        PrintableMetricRows = [];
        SortOptions = [];
        RecentIssueSubmissions = [];

        _initializeCommand = new AsyncCommand(async () => await RunBusyAsync(InitializeAsync));
        _applyFiltersCommand = new AsyncCommand(async () => await RunBusyAsync(ApplyFiltersInternalAsync));
        _resetFiltersCommand = new RelayCommand(ResetFilters);
        _setCustomerPaymentModeCommand = new RelayCommand(() => SetMode(ReportMode.CustomerPayments));
        _setIssueModeCommand = new RelayCommand(() => SetMode(ReportMode.Issues));
        _setPrintModeCommand = new RelayCommand(() => SetMode(ReportMode.Printables));
        _setLast30DaysCommand = new RelayCommand(() => SetQuickRange(30));
        _setLast90DaysCommand = new RelayCommand(() => SetQuickRange(90));
        _setLast12MonthsCommand = new RelayCommand(() => SetQuickRange(365));
        _exportCurrentReportCommand = new AsyncCommand(async () => await RunBusyAsync(ct => ExportCurrentReportAsync(false, ct)));
        _printCurrentReportCommand = new AsyncCommand(async () => await RunBusyAsync(ct => ExportCurrentReportAsync(true, ct)));
        _exportRevenueCsvCommand = new AsyncCommand(async () => await RunBusyAsync(ct => ExportSingleModeCsvAsync("revenue", ct)));
        _exportOverdueCsvCommand = new AsyncCommand(async () => await RunBusyAsync(ct => ExportSingleModeCsvAsync("overdue", ct)));
        _exportPaymentsCsvCommand = new AsyncCommand(async () => await RunBusyAsync(ct => ExportSingleModeCsvAsync("payments", ct)));

        UpdateSortOptionsForMode();
        RaiseModeProperties();
    }

    public ObservableCollection<ReportRevenueRowItem> RevenueBreakdownRows { get; }

    public ObservableCollection<ReportIssueTimelineRowItem> IssueTimelineRows { get; }

    public ObservableCollection<ReportPrintableMetricRowItem> PrintableMetricRows { get; }

    public ObservableCollection<string> SortOptions { get; }

    public ObservableCollection<string> RecentIssueSubmissions { get; }

    public IReadOnlyList<string> SortDirections { get; } = ["Ascending", "Descending"];

    public IReadOnlyList<string> GroupByOptions { get; } = ["Monthly", "Weekly", "Daily"];

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

    public string CustomerFilterText
    {
        get => _customerFilterText;
        set => SetProperty(ref _customerFilterText, value);
    }

    public string GroupBySelection
    {
        get => _groupBySelection;
        set => SetProperty(ref _groupBySelection, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            if (_suppressSearchSortRefresh)
            {
                return;
            }

            ApplyCurrentModeSearchSort();
        }
    }

    public string SelectedSortColumn
    {
        get => _selectedSortColumn;
        set
        {
            if (!SetProperty(ref _selectedSortColumn, value))
            {
                return;
            }

            if (_suppressSearchSortRefresh)
            {
                return;
            }

            ApplyCurrentModeSearchSort();
        }
    }

    public string SelectedSortDirection
    {
        get => _selectedSortDirection;
        set
        {
            if (!SetProperty(ref _selectedSortDirection, value))
            {
                return;
            }

            if (_suppressSearchSortRefresh)
            {
                return;
            }

            ApplyCurrentModeSearchSort();
        }
    }

    public decimal CollectedAmount
    {
        get => _collectedAmount;
        private set
        {
            if (!SetProperty(ref _collectedAmount, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CollectedAmountText));
        }
    }

    public decimal TotalBilledAmount
    {
        get => _totalBilledAmount;
        private set
        {
            if (!SetProperty(ref _totalBilledAmount, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(TotalBilledAmountText));
        }
    }

    public decimal VarianceAmount
    {
        get => _varianceAmount;
        private set
        {
            if (!SetProperty(ref _varianceAmount, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(VarianceAmountText));
        }
    }

    public int TotalIssues
    {
        get => _totalIssues;
        private set => SetProperty(ref _totalIssues, value);
    }

    public int PriorityIssues
    {
        get => _priorityIssues;
        private set => SetProperty(ref _priorityIssues, value);
    }

    public int CompletedIssues
    {
        get => _completedIssues;
        private set => SetProperty(ref _completedIssues, value);
    }

    public string IssueStatusSummary
    {
        get => _issueStatusSummary;
        private set => SetProperty(ref _issueStatusSummary, value);
    }

    public string IssueCategorySummary
    {
        get => _issueCategorySummary;
        private set => SetProperty(ref _issueCategorySummary, value);
    }

    public string LastExportPath
    {
        get => _lastExportPath;
        private set => SetProperty(ref _lastExportPath, value);
    }

    public string LastExportType
    {
        get => _lastExportType;
        private set => SetProperty(ref _lastExportType, value);
    }

    public int LastExportRowCount
    {
        get => _lastExportRowCount;
        private set => SetProperty(ref _lastExportRowCount, value);
    }

    public DateTime? LastExportedAtUtc
    {
        get => _lastExportedAtUtc;
        private set => SetProperty(ref _lastExportedAtUtc, value);
    }

    public string CollectedAmountText => $"P{CollectedAmount:N2}";

    public string TotalBilledAmountText => $"P{TotalBilledAmount:N2}";

    public string VarianceAmountText => $"P{VarianceAmount:N2}";

    public bool IsCustomerPaymentMode => _selectedMode == ReportMode.CustomerPayments;

    public bool IsIssueMode => _selectedMode == ReportMode.Issues;

    public bool IsPrintMode => _selectedMode == ReportMode.Printables;

    public string HeaderTitle => _selectedMode switch
    {
        ReportMode.Issues => "Issue Report",
        ReportMode.Printables => "Printable Reports",
        _ => "Customer Payment Report"
    };

    public string HeaderSubtitle => _selectedMode switch
    {
        ReportMode.Issues => "Track reported customer issues, priorities, and resolutions across the selected period.",
        ReportMode.Printables => "Generate printer-friendly revenue summaries and operational metrics for archival use.",
        _ => "Monitor collections, compare billed against paid invoices, and drill into customer payments."
    };

    public string CustomerPaymentActionBackground => _selectedMode == ReportMode.CustomerPayments ? "#FFFFFF" : "#4D84EA";

    public string CustomerPaymentActionTextColor => _selectedMode == ReportMode.CustomerPayments ? "#2E5FD2" : "#EAF3FF";

    public string IssueActionBackground => _selectedMode == ReportMode.Issues ? "#FFFFFF" : "#4D84EA";

    public string IssueActionTextColor => _selectedMode == ReportMode.Issues ? "#2E5FD2" : "#EAF3FF";

    public string PrintActionBackground => _selectedMode == ReportMode.Printables ? "#FFFFFF" : "#4D84EA";

    public string PrintActionTextColor => _selectedMode == ReportMode.Printables ? "#2E5FD2" : "#EAF3FF";

    public string SearchPlaceholder => _selectedMode switch
    {
        ReportMode.Issues => "Type to filter issue timeline...",
        ReportMode.Printables => "Type to filter rows...",
        _ => "Type to filter rows..."
    };

    public bool HasRevenueRows => RevenueBreakdownRows.Count > 0;

    public bool HasIssueTimelineRows => IssueTimelineRows.Count > 0;

    public bool HasPrintableRows => PrintableMetricRows.Count > 0;

    public string RevenueEmptyMessage => "No data for the selected filters.";

    public string IssueTimelineEmptyMessage => "No timeline data for the selected dates.";

    public string PrintableEmptyMessage => "No printable metrics found for the selected filters.";

    public AsyncCommand InitializeCommand => _initializeCommand;

    public AsyncCommand ApplyFiltersCommand => _applyFiltersCommand;

    public RelayCommand ResetFiltersCommand => _resetFiltersCommand;

    public RelayCommand SetCustomerPaymentModeCommand => _setCustomerPaymentModeCommand;

    public RelayCommand SetIssueModeCommand => _setIssueModeCommand;

    public RelayCommand SetPrintModeCommand => _setPrintModeCommand;

    public RelayCommand SetLast30DaysCommand => _setLast30DaysCommand;

    public RelayCommand SetLast90DaysCommand => _setLast90DaysCommand;

    public RelayCommand SetLast12MonthsCommand => _setLast12MonthsCommand;

    public AsyncCommand ExportCurrentReportCommand => _exportCurrentReportCommand;

    public AsyncCommand PrintCurrentReportCommand => _printCurrentReportCommand;

    public AsyncCommand ExportRevenueCsvCommand => _exportRevenueCsvCommand;

    public AsyncCommand ExportOverdueCsvCommand => _exportOverdueCsvCommand;

    public AsyncCommand ExportPaymentsCsvCommand => _exportPaymentsCsvCommand;
}
