using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class PaymentsViewModel : BaseViewModel
{
    private readonly IPaymentService _paymentService;
    private readonly IBillingService _billingService;
    private readonly ICustomerService _customerService;
    private readonly IDialogService _dialogService;
    private readonly AppStateStore _stateStore;

    private readonly AsyncCommand _searchBillCommand;
    private readonly AsyncCommand _processPaymentCommand;
    private readonly AsyncCommand _refreshHistoryCommand;

    private Guid _selectedBillId;
    private string _billSearchText = string.Empty;
    private string _accountPreview = "--";
    private string _currentBillNumber = "--";
    private decimal _currentBalance;
    private int _unpaidBills;
    private decimal _latestBillAmount;
    private string _latestBillLabel = "--";
    private string _dueDateDisplay = "--";
    private string _statusText = "No bill selected";
    private string _statusBackground = "#EEF3FB";
    private string _statusForeground = "#41556D";
    private decimal _amountTendered;
    private string _reference = string.Empty;
    private string _searchGuide = "Guide: Search an account, review unpaid bills, then process payment.";

    public PaymentsViewModel()
        : this(
            App.Services.GetRequiredService<IPaymentService>(),
            App.Services.GetRequiredService<IBillingService>(),
            App.Services.GetRequiredService<ICustomerService>(),
            App.Services.GetRequiredService<IDialogService>(),
            App.Services.GetRequiredService<AppStateStore>())
    {
    }

    public PaymentsViewModel(
        IPaymentService paymentService,
        IBillingService billingService,
        ICustomerService customerService,
        IDialogService dialogService,
        AppStateStore stateStore)
    {
        _paymentService = paymentService;
        _billingService = billingService;
        _customerService = customerService;
        _dialogService = dialogService;
        _stateStore = stateStore;

        _searchBillCommand = new AsyncCommand(async () => await RunBusyAsync(LoadBillSnapshotAsync));
        _processPaymentCommand = new AsyncCommand(async () => await RunBusyAsync(ProcessPaymentAsync), () => CanProcessPayment);
        _refreshHistoryCommand = new AsyncCommand(async () => await RunBusyAsync(RefreshHistoryAsync), () => HasSelectedBill);
    }

    public ObservableCollection<PaymentHistoryRowItem> PaymentHistory { get; } = [];

    public string BillSearchText
    {
        get => _billSearchText;
        set => SetProperty(ref _billSearchText, value);
    }

    public string AccountPreview
    {
        get => _accountPreview;
        private set => SetProperty(ref _accountPreview, value);
    }

    public string CurrentBillNumber
    {
        get => _currentBillNumber;
        private set => SetProperty(ref _currentBillNumber, value);
    }

    public decimal CurrentBalance
    {
        get => _currentBalance;
        private set
        {
            if (!SetProperty(ref _currentBalance, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CurrentBalanceText));
            RaisePropertyChanged(nameof(AmountToApply));
            RaisePropertyChanged(nameof(ChangeAmount));
            RaisePropertyChanged(nameof(CanProcessPayment));
            _processPaymentCommand.RaiseCanExecuteChanged();
        }
    }

    public string CurrentBalanceText => $"P{CurrentBalance:N2}";

    public int UnpaidBills
    {
        get => _unpaidBills;
        private set => SetProperty(ref _unpaidBills, value);
    }

    public decimal LatestBillAmount
    {
        get => _latestBillAmount;
        private set => SetProperty(ref _latestBillAmount, value);
    }

    public string LatestBillText => $"P{LatestBillAmount:N2}";

    public string LatestBillLabel
    {
        get => _latestBillLabel;
        private set => SetProperty(ref _latestBillLabel, value);
    }

    public string DueDateDisplay
    {
        get => _dueDateDisplay;
        private set => SetProperty(ref _dueDateDisplay, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusBackground
    {
        get => _statusBackground;
        private set => SetProperty(ref _statusBackground, value);
    }

    public string StatusForeground
    {
        get => _statusForeground;
        private set => SetProperty(ref _statusForeground, value);
    }

    public decimal AmountTendered
    {
        get => _amountTendered;
        set
        {
            if (!SetProperty(ref _amountTendered, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(AmountToApply));
            RaisePropertyChanged(nameof(ChangeAmount));
            RaisePropertyChanged(nameof(CanProcessPayment));
            _processPaymentCommand.RaiseCanExecuteChanged();
        }
    }

    public decimal AmountToApply => Math.Min(Math.Max(0m, AmountTendered), CurrentBalance);

    public decimal ChangeAmount => Math.Max(0m, AmountTendered - AmountToApply);

    public string Reference
    {
        get => _reference;
        set => SetProperty(ref _reference, value);
    }

    public string SearchGuide
    {
        get => _searchGuide;
        private set => SetProperty(ref _searchGuide, value);
    }

    public bool HasSelectedBill => _selectedBillId != Guid.Empty;

    public bool HasPaymentHistory => PaymentHistory.Count > 0;

    public bool CanProcessPayment => HasSelectedBill && AmountToApply > 0m;

    public string HistorySummary => HasPaymentHistory
        ? $"Showing {PaymentHistory.Count} payment record(s)."
        : "No payments recorded yet for this bill.";

    public AsyncCommand SearchBillCommand => _searchBillCommand;

    public AsyncCommand ProcessPaymentCommand => _processPaymentCommand;

    public AsyncCommand RefreshHistoryCommand => _refreshHistoryCommand;

    private async Task LoadBillSnapshotAsync()
    {
        var term = BillSearchText.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            await _dialogService.AlertAsync("Search", "Enter a bill number, bill GUID, or customer name.");
            return;
        }

        var bills = await _billingService.GetBillsAsync();
        if (bills.Count == 0)
        {
            await _dialogService.AlertAsync("Search", "No bills are available yet.");
            return;
        }

        var customers = await _customerService.SearchCustomersAsync(null);
        var customersById = customers.ToDictionary(x => x.Id, x => x);

        var selected = ResolveBillBySearch(term, bills, customersById);
        if (selected is null)
        {
            ResetSelectedBillState();
            StatusMessage = "No bill matched your search.";
            await _dialogService.AlertAsync("Search", "No matching bill or customer account was found.");
            return;
        }

        ApplySelectedBill(selected, bills, customersById);
        await RefreshHistoryForBillAsync(selected.Id);
        StatusMessage = $"Loaded bill {selected.BillNumber}.";
    }

    private async Task ProcessPaymentAsync()
    {
        if (!HasSelectedBill)
        {
            return;
        }

        if (AmountToApply <= 0m)
        {
            await _dialogService.AlertAsync("Validation", "Payment amount must be greater than zero.");
            return;
        }

        var payment = new Payment
        {
            BillId = _selectedBillId,
            Amount = AmountToApply,
            ReferenceNumber = Reference,
            CreatedByUserId = _stateStore.Session?.UserId ?? Guid.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };

        var saved = await _paymentService.RecordPaymentAsync(payment);
        PaymentHistory.Insert(0, MapPaymentRow(saved, CurrentBillNumber));
        RaisePropertyChanged(nameof(HasPaymentHistory));
        RaisePropertyChanged(nameof(HistorySummary));

        var cashChange = ChangeAmount;
        await RefreshSelectedBillByIdAsync(_selectedBillId);

        var changeText = cashChange > 0m ? $" Change: P{cashChange:N2}." : string.Empty;
        await _dialogService.AlertAsync("Payment", $"Payment posted successfully.{changeText}");
    }

    private async Task RefreshHistoryAsync()
    {
        if (!HasSelectedBill)
        {
            return;
        }

        await RefreshHistoryForBillAsync(_selectedBillId);
    }

    private async Task RefreshSelectedBillByIdAsync(Guid billId)
    {
        var bills = await _billingService.GetBillsAsync();
        var selected = bills.FirstOrDefault(x => x.Id == billId);
        if (selected is null)
        {
            ResetSelectedBillState();
            return;
        }

        var customers = await _customerService.SearchCustomersAsync(null);
        var customersById = customers.ToDictionary(x => x.Id, x => x);
        ApplySelectedBill(selected, bills, customersById);
        await RefreshHistoryForBillAsync(billId);
    }

    private async Task RefreshHistoryForBillAsync(Guid billId)
    {
        var payments = await _paymentService.GetPaymentsByBillAsync(billId);

        PaymentHistory.Clear();
        foreach (var row in payments
                     .OrderByDescending(x => x.PaymentDateUtc)
                     .Select(payment => MapPaymentRow(payment, CurrentBillNumber)))
        {
            PaymentHistory.Add(row);
        }

        RaisePropertyChanged(nameof(HasPaymentHistory));
        RaisePropertyChanged(nameof(HistorySummary));
    }

    private void ApplySelectedBill(
        Bill bill,
        IReadOnlyList<Bill> allBills,
        IReadOnlyDictionary<Guid, Customer> customersById)
    {
        _selectedBillId = bill.Id;
        RaisePropertyChanged(nameof(HasSelectedBill));
        RaisePropertyChanged(nameof(CanProcessPayment));
        _processPaymentCommand.RaiseCanExecuteChanged();
        _refreshHistoryCommand.RaiseCanExecuteChanged();

        CurrentBillNumber = bill.BillNumber;
        CurrentBalance = bill.Balance;
        DueDateDisplay = bill.DueDateUtc.ToString("MMM dd, yyyy");
        if (string.IsNullOrWhiteSpace(Reference))
        {
            Reference = $"PMT-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        customersById.TryGetValue(bill.CustomerId, out var customer);
        var shortAccount = bill.CustomerId.ToString()[..8].ToUpperInvariant();
        AccountPreview = customer is null
            ? $"Account {shortAccount}"
            : $"{customer.Name} ({shortAccount})";

        var customerBills = allBills.Where(x => x.CustomerId == bill.CustomerId).ToList();
        UnpaidBills = customerBills.Count(x => x.Status != BillStatus.Paid && x.Balance > 0m);

        var latestBill = customerBills
            .OrderByDescending(x => x.DueDateUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
        LatestBillAmount = latestBill?.Amount ?? bill.Amount;
        RaisePropertyChanged(nameof(LatestBillText));
        LatestBillLabel = latestBill?.BillNumber ?? bill.BillNumber;

        var (statusText, background, foreground) = ResolveBillStatusAppearance(bill.Status, bill.Balance);
        StatusText = statusText;
        StatusBackground = background;
        StatusForeground = foreground;

        AmountTendered = bill.Balance;
    }

    private void ResetSelectedBillState()
    {
        _selectedBillId = Guid.Empty;
        RaisePropertyChanged(nameof(HasSelectedBill));
        RaisePropertyChanged(nameof(CanProcessPayment));
        _processPaymentCommand.RaiseCanExecuteChanged();
        _refreshHistoryCommand.RaiseCanExecuteChanged();

        AccountPreview = "--";
        CurrentBillNumber = "--";
        CurrentBalance = 0m;
        UnpaidBills = 0;
        LatestBillAmount = 0m;
        RaisePropertyChanged(nameof(LatestBillText));
        LatestBillLabel = "--";
        DueDateDisplay = "--";
        StatusText = "No bill selected";
        StatusBackground = "#EEF3FB";
        StatusForeground = "#41556D";
        AmountTendered = 0m;

        PaymentHistory.Clear();
        RaisePropertyChanged(nameof(HasPaymentHistory));
        RaisePropertyChanged(nameof(HistorySummary));
    }

    private static Bill? ResolveBillBySearch(
        string term,
        IReadOnlyList<Bill> bills,
        IReadOnlyDictionary<Guid, Customer> customersById)
    {
        if (Guid.TryParse(term, out var parsedGuid))
        {
            var guidMatch = bills.FirstOrDefault(x => x.Id == parsedGuid)
                ?? bills.FirstOrDefault(x => x.CustomerId == parsedGuid);
            if (guidMatch is not null)
            {
                return guidMatch;
            }
        }

        var exactBillNumber = bills.FirstOrDefault(x =>
            string.Equals(x.BillNumber, term, StringComparison.OrdinalIgnoreCase));
        if (exactBillNumber is not null)
        {
            return exactBillNumber;
        }

        var containsBillNumber = bills
            .Where(x => x.BillNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Status == BillStatus.Paid ? 1 : 0)
            .ThenByDescending(x => x.DueDateUtc)
            .FirstOrDefault();
        if (containsBillNumber is not null)
        {
            return containsBillNumber;
        }

        var matchedCustomerIds = customersById.Values
            .Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(x.PhoneNumber) && x.PhoneNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Id)
            .ToHashSet();

        if (matchedCustomerIds.Count == 0)
        {
            return null;
        }

        return bills
            .Where(x => matchedCustomerIds.Contains(x.CustomerId))
            .OrderBy(x => x.Status == BillStatus.Paid ? 1 : 0)
            .ThenByDescending(x => x.DueDateUtc)
            .FirstOrDefault();
    }

    private static PaymentHistoryRowItem MapPaymentRow(Payment payment, string billNumber)
    {
        var (statusText, background, foreground) = ResolvePaymentStatusAppearance(payment.Status);
        return new PaymentHistoryRowItem
        {
            PaymentDateUtc = payment.PaymentDateUtc,
            PaymentDateDisplay = payment.PaymentDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            BillNumber = billNumber,
            ReferenceNumber = string.IsNullOrWhiteSpace(payment.ReferenceNumber) ? "-" : payment.ReferenceNumber,
            Amount = payment.Amount,
            AmountDisplay = $"P{payment.Amount:N2}",
            StatusText = statusText,
            StatusBackground = background,
            StatusForeground = foreground
        };
    }

    private static (string Text, string Background, string Foreground) ResolveBillStatusAppearance(BillStatus status, decimal balance)
    {
        if (status == BillStatus.Paid || balance <= 0m)
        {
            return ("Settled", "#E8F8EE", "#1F8A57");
        }

        return status switch
        {
            BillStatus.Overdue => ("Overdue", "#FFE8E8", "#C23B3B"),
            _ => ("Pending", "#FFF3D9", "#AF6B00")
        };
    }

    private static (string Text, string Background, string Foreground) ResolvePaymentStatusAppearance(PaymentStatus status)
    {
        return status switch
        {
            PaymentStatus.Completed => ("Completed", "#E8F8EE", "#1F8A57"),
            PaymentStatus.Failed => ("Failed", "#FFE8E8", "#C23B3B"),
            PaymentStatus.Reversed => ("Reversed", "#EEF1F6", "#58667A"),
            _ => ("Pending", "#FFF3D9", "#AF6B00")
        };
    }
}
