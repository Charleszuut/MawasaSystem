using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class BillingViewModel : BaseViewModel
{
    private const int LedgerPageSize = 5;
    private readonly IBillingService _billingService;
    private readonly ICustomerService _customerService;
    private readonly AppStateStore _stateStore;
    private readonly IDialogService _dialogService;

    private readonly List<BillingLedgerRowItem> _allLedgerRows = [];
    private readonly List<BillingLedgerRowItem> _filteredLedgerRows = [];

    private readonly AsyncCommand _createBillCommand;
    private readonly AsyncCommand _refreshLedgerCommand;
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;
    private readonly RelayCommand _setAllFilterCommand;
    private readonly RelayCommand _setPendingFilterCommand;
    private readonly RelayCommand _setPrintedFilterCommand;

    private string _billNumber = string.Empty;
    private string _customerIdText = string.Empty;
    private DateTime _invoiceDateUtc = DateTime.UtcNow;
    private string _personInCharge = "Staff";
    private decimal _previousReading;
    private decimal _currentReading;
    private decimal _baseRate = 25m;
    private decimal _maintenanceCharge;
    private DateTime _billingFromDateUtc = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    private DateTime _billingToDateUtc = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month));
    private DateTime _dueDateUtc = DateTime.UtcNow.AddDays(14);
    private DateTime _disconnectionDateUtc = DateTime.UtcNow.AddDays(30);
    private string _searchText = string.Empty;
    private int _pendingPrintCount;
    private int _printedLockedCount;
    private decimal _collectionsToday;
    private decimal _outstandingBalance;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _filteredLedgerCount;
    private BillingLedgerFilter _selectedLedgerFilter = BillingLedgerFilter.All;

    public BillingViewModel()
        : this(
            App.Services.GetRequiredService<IBillingService>(),
            App.Services.GetRequiredService<ICustomerService>(),
            App.Services.GetRequiredService<AppStateStore>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public BillingViewModel(
        IBillingService billingService,
        ICustomerService customerService,
        AppStateStore stateStore,
        IDialogService dialogService)
    {
        _billingService = billingService;
        _customerService = customerService;
        _stateStore = stateStore;
        _dialogService = dialogService;

        _createBillCommand = new AsyncCommand(async () => await RunBusyAsync(CreateBillInternalAsync));
        _refreshLedgerCommand = new AsyncCommand(async () => await RunBusyAsync(LoadLedgerAsync));
        _previousPageCommand = new RelayCommand(MoveToPreviousPage, () => CanGoPreviousPage);
        _nextPageCommand = new RelayCommand(MoveToNextPage, () => CanGoNextPage);
        _setAllFilterCommand = new RelayCommand(() => SetFilter(BillingLedgerFilter.All));
        _setPendingFilterCommand = new RelayCommand(() => SetFilter(BillingLedgerFilter.PendingPrint));
        _setPrintedFilterCommand = new RelayCommand(() => SetFilter(BillingLedgerFilter.Printed));

        LedgerRows = [];
        PersonInCharge = _stateStore.Session?.Username ?? "Staff";
        BillNumber = GenerateBillNumber();
        RecalculateComputedTotals();
        ApplyLedgerFilters(resetToFirstPage: true);
    }

    public ObservableCollection<BillingLedgerRowItem> LedgerRows { get; }

    public string BillNumber
    {
        get => _billNumber;
        set => SetProperty(ref _billNumber, value);
    }

    public string CustomerIdText
    {
        get => _customerIdText;
        set => SetProperty(ref _customerIdText, value);
    }

    public DateTime InvoiceDateUtc
    {
        get => _invoiceDateUtc;
        set => SetProperty(ref _invoiceDateUtc, value);
    }

    public string PersonInCharge
    {
        get => _personInCharge;
        private set => SetProperty(ref _personInCharge, value);
    }

    public decimal PreviousReading
    {
        get => _previousReading;
        set
        {
            if (!SetProperty(ref _previousReading, value))
            {
                return;
            }

            RecalculateComputedTotals();
        }
    }

    public decimal CurrentReading
    {
        get => _currentReading;
        set
        {
            if (!SetProperty(ref _currentReading, value))
            {
                return;
            }

            RecalculateComputedTotals();
        }
    }

    public decimal BaseRate
    {
        get => _baseRate;
        set
        {
            if (!SetProperty(ref _baseRate, value))
            {
                return;
            }

            RecalculateComputedTotals();
        }
    }

    public decimal MaintenanceCharge
    {
        get => _maintenanceCharge;
        set
        {
            if (!SetProperty(ref _maintenanceCharge, value))
            {
                return;
            }

            RecalculateComputedTotals();
        }
    }

    public decimal Consumption => Math.Max(0m, CurrentReading - PreviousReading);

    public decimal Subtotal => Math.Round(Consumption * BaseRate, 2);

    public decimal VatAmount => Math.Round(Subtotal * 0.12m, 2);

    public decimal TotalAmountDue => Math.Max(0m, Math.Round(Subtotal + VatAmount + MaintenanceCharge, 2));

    public DateTime BillingFromDateUtc
    {
        get => _billingFromDateUtc;
        set => SetProperty(ref _billingFromDateUtc, value);
    }

    public DateTime BillingToDateUtc
    {
        get => _billingToDateUtc;
        set => SetProperty(ref _billingToDateUtc, value);
    }

    public DateTime DueDateUtc
    {
        get => _dueDateUtc;
        set
        {
            if (!SetProperty(ref _dueDateUtc, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(DueDateDisplay));
            if (DisconnectionDateUtc < value)
            {
                DisconnectionDateUtc = value.AddDays(1);
            }
        }
    }

    public DateTime DisconnectionDateUtc
    {
        get => _disconnectionDateUtc;
        set
        {
            if (!SetProperty(ref _disconnectionDateUtc, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(DisconnectionDateDisplay));
        }
    }

    public string DueDateDisplay => DueDateUtc.ToString("MMM d, yyyy");

    public string DisconnectionDateDisplay => DisconnectionDateUtc.ToString("MMM d, yyyy");

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            ApplyLedgerFilters(resetToFirstPage: true);
        }
    }

    public int PendingPrintCount
    {
        get => _pendingPrintCount;
        private set => SetProperty(ref _pendingPrintCount, value);
    }

    public int PrintedLockedCount
    {
        get => _printedLockedCount;
        private set => SetProperty(ref _printedLockedCount, value);
    }

    public decimal CollectionsToday
    {
        get => _collectionsToday;
        private set => SetProperty(ref _collectionsToday, value);
    }

    public decimal OutstandingBalance
    {
        get => _outstandingBalance;
        private set => SetProperty(ref _outstandingBalance, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!SetProperty(ref _currentPage, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(PageSummary));
            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            RaisePropertyChanged(nameof(LedgerCountSummary));
            UpdatePagingCommands();
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (!SetProperty(ref _totalPages, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(PageSummary));
            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            UpdatePagingCommands();
        }
    }

    public bool CanGoPreviousPage => CurrentPage > 1;

    public bool CanGoNextPage => CurrentPage < TotalPages;

    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";

    public string LedgerCountSummary
    {
        get
        {
            if (_filteredLedgerCount == 0)
            {
                return "No ledger rows found.";
            }

            var start = ((CurrentPage - 1) * LedgerPageSize) + 1;
            var end = Math.Min(CurrentPage * LedgerPageSize, _filteredLedgerCount);
            return $"Showing {start}-{end} of {_filteredLedgerCount} rows";
        }
    }

    public string AllFilterBackground => _selectedLedgerFilter == BillingLedgerFilter.All ? "#2D6DE2" : "#EEF3FB";

    public string AllFilterTextColor => _selectedLedgerFilter == BillingLedgerFilter.All ? "#FFFFFF" : "#405367";

    public string PendingFilterBackground => _selectedLedgerFilter == BillingLedgerFilter.PendingPrint ? "#2D6DE2" : "#EEF3FB";

    public string PendingFilterTextColor => _selectedLedgerFilter == BillingLedgerFilter.PendingPrint ? "#FFFFFF" : "#405367";

    public string PrintedFilterBackground => _selectedLedgerFilter == BillingLedgerFilter.Printed ? "#2D6DE2" : "#EEF3FB";

    public string PrintedFilterTextColor => _selectedLedgerFilter == BillingLedgerFilter.Printed ? "#FFFFFF" : "#405367";

    public AsyncCommand CreateBillCommand => _createBillCommand;

    public AsyncCommand RefreshLedgerCommand => _refreshLedgerCommand;

    public RelayCommand PreviousPageCommand => _previousPageCommand;

    public RelayCommand NextPageCommand => _nextPageCommand;

    public RelayCommand SetAllFilterCommand => _setAllFilterCommand;

    public RelayCommand SetPendingFilterCommand => _setPendingFilterCommand;

    public RelayCommand SetPrintedFilterCommand => _setPrintedFilterCommand;

    private async Task CreateBillInternalAsync()
    {
        if (!Guid.TryParse(CustomerIdText.Trim(), out var customerId))
        {
            await _dialogService.AlertAsync("Validation", "Customer account must be a valid customer GUID.");
            return;
        }

        if (CurrentReading < PreviousReading)
        {
            await _dialogService.AlertAsync("Validation", "Current reading cannot be less than previous reading.");
            return;
        }

        if (TotalAmountDue <= 0m)
        {
            await _dialogService.AlertAsync("Validation", "Total amount due must be greater than zero.");
            return;
        }

        if (string.IsNullOrWhiteSpace(BillNumber))
        {
            BillNumber = GenerateBillNumber();
        }

        var userId = _stateStore.Session?.UserId ?? Guid.Empty;
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            BillNumber = BillNumber.Trim(),
            CustomerId = customerId,
            Amount = TotalAmountDue,
            Balance = TotalAmountDue,
            DueDateUtc = DueDateUtc,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _billingService.CreateBillAsync(bill);
        await _dialogService.AlertAsync("Billing", $"Bill {bill.BillNumber} was saved.");

        BillNumber = GenerateBillNumber();
        InvoiceDateUtc = DateTime.UtcNow;
        PreviousReading = CurrentReading;
        StatusMessage = "Bill saved successfully.";

        await LoadLedgerAsync();
    }

    private async Task LoadLedgerAsync()
    {
        PersonInCharge = _stateStore.Session?.Username ?? "Staff";

        var bills = await _billingService.GetBillsAsync();
        var customers = await _customerService.SearchCustomersAsync(null);
        var customerById = customers.ToDictionary(c => c.Id, c => c);

        _allLedgerRows.Clear();
        foreach (var bill in bills)
        {
            _allLedgerRows.Add(MapLedgerRow(bill, customerById));
        }

        PendingPrintCount = bills.Count(b => b.Status != BillStatus.Paid);
        PrintedLockedCount = bills.Count(b => b.Status == BillStatus.Paid);
        CollectionsToday = bills
            .Where(b => b.PaidAtUtc.HasValue && b.PaidAtUtc.Value.Date == DateTime.UtcNow.Date)
            .Sum(b => b.Amount);
        OutstandingBalance = bills.Sum(b => b.Balance);

        ApplyLedgerFilters(resetToFirstPage: true);
        StatusMessage = $"Loaded {_allLedgerRows.Count} ledger row(s).";
    }

    private BillingLedgerRowItem MapLedgerRow(Bill bill, IReadOnlyDictionary<Guid, Customer> customerById)
    {
        customerById.TryGetValue(bill.CustomerId, out var customer);
        var customerName = customer?.Name ?? "Unknown customer";
        var address = string.IsNullOrWhiteSpace(customer?.Address) ? "Address not set" : customer!.Address!;
        var accountNumber = (customer?.Id ?? bill.CustomerId).ToString()[..8].ToUpperInvariant();

        var periodEnd = new DateTime(bill.DueDateUtc.Year, bill.DueDateUtc.Month, 1).AddDays(-1);
        var periodStart = new DateTime(periodEnd.Year, periodEnd.Month, 1);
        var billingPeriod = $"{periodStart:MMM dd} - {periodEnd:MMM dd, yyyy}";

        var (billStatusText, billStatusBackground, billStatusForeground) = ResolveBillStatusAppearance(bill.Status);
        var (printStatusText, printStatusBackground, printStatusForeground) = ResolvePrintStatusAppearance(bill.Status);

        return new BillingLedgerRowItem
        {
            BillId = bill.Id,
            BillNumber = bill.BillNumber,
            CustomerDisplayName = customerName,
            AccountNumber = accountNumber,
            Address = address,
            BillingPeriod = billingPeriod,
            DueDateUtc = bill.DueDateUtc,
            DueDateDisplay = bill.DueDateUtc.ToString("yyyy-MM-dd"),
            Amount = bill.Amount,
            Balance = bill.Balance,
            BillStatusText = billStatusText,
            BillStatusBackground = billStatusBackground,
            BillStatusForeground = billStatusForeground,
            PrintStatusText = printStatusText,
            PrintStatusBackground = printStatusBackground,
            PrintStatusForeground = printStatusForeground
        };
    }

    private static (string Text, string Background, string Foreground) ResolveBillStatusAppearance(BillStatus status)
    {
        return status switch
        {
            BillStatus.Paid => ("Paid", "#E8F8EE", "#1F8A57"),
            BillStatus.Overdue => ("Overdue", "#FFE8E8", "#C23B3B"),
            _ => ("Pending", "#FFF3D9", "#AF6B00")
        };
    }

    private static (string Text, string Background, string Foreground) ResolvePrintStatusAppearance(BillStatus status)
    {
        return status switch
        {
            BillStatus.Paid => ("Printed", "#E8F8EE", "#1F8A57"),
            BillStatus.Overdue => ("Awaiting print", "#EAF0FF", "#2F5DC7"),
            _ => ("Pending print", "#EEF3FB", "#4A5D76")
        };
    }

    private void ApplyLedgerFilters(bool resetToFirstPage)
    {
        IEnumerable<BillingLedgerRowItem> query = _allLedgerRows;

        query = _selectedLedgerFilter switch
        {
            BillingLedgerFilter.PendingPrint => query.Where(x => !string.Equals(x.PrintStatusText, "Printed", StringComparison.OrdinalIgnoreCase)),
            BillingLedgerFilter.Printed => query.Where(x => string.Equals(x.PrintStatusText, "Printed", StringComparison.OrdinalIgnoreCase)),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(row =>
                row.CustomerDisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.BillNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.AccountNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Address.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        _filteredLedgerRows.Clear();
        _filteredLedgerRows.AddRange(query
            .OrderByDescending(x => x.DueDateUtc)
            .ThenBy(x => x.CustomerDisplayName));
        _filteredLedgerCount = _filteredLedgerRows.Count;

        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        TotalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(1, _filteredLedgerCount) / (double)LedgerPageSize));
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        RebuildCurrentPageRows();
        RaisePropertyChanged(nameof(LedgerCountSummary));
    }

    private void RebuildCurrentPageRows()
    {
        LedgerRows.Clear();
        if (_filteredLedgerRows.Count == 0)
        {
            return;
        }

        var skip = (CurrentPage - 1) * LedgerPageSize;
        foreach (var row in _filteredLedgerRows.Skip(skip).Take(LedgerPageSize))
        {
            LedgerRows.Add(row);
        }
    }

    private void MoveToPreviousPage()
    {
        if (!CanGoPreviousPage)
        {
            return;
        }

        CurrentPage--;
        RebuildCurrentPageRows();
    }

    private void MoveToNextPage()
    {
        if (!CanGoNextPage)
        {
            return;
        }

        CurrentPage++;
        RebuildCurrentPageRows();
    }

    private void SetFilter(BillingLedgerFilter filter)
    {
        if (_selectedLedgerFilter == filter)
        {
            return;
        }

        _selectedLedgerFilter = filter;
        RaisePropertyChanged(nameof(AllFilterBackground));
        RaisePropertyChanged(nameof(AllFilterTextColor));
        RaisePropertyChanged(nameof(PendingFilterBackground));
        RaisePropertyChanged(nameof(PendingFilterTextColor));
        RaisePropertyChanged(nameof(PrintedFilterBackground));
        RaisePropertyChanged(nameof(PrintedFilterTextColor));
        ApplyLedgerFilters(resetToFirstPage: true);
    }

    private void RecalculateComputedTotals()
    {
        RaisePropertyChanged(nameof(Consumption));
        RaisePropertyChanged(nameof(Subtotal));
        RaisePropertyChanged(nameof(VatAmount));
        RaisePropertyChanged(nameof(TotalAmountDue));
    }

    private void UpdatePagingCommands()
    {
        _previousPageCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
    }

    private static string GenerateBillNumber()
    {
        return $"INV-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
    }

    private enum BillingLedgerFilter
    {
        All = 0,
        PendingPrint = 1,
        Printed = 2
    }
}
