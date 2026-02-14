using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class CustomersViewModel : BaseViewModel
{
    private const int PageSize = 10;

    private readonly ICustomerService _customerService;
    private readonly IBillingService _billingService;
    private readonly IDialogService _dialogService;

    private readonly List<CustomerGridRowItem> _allRows = [];
    private readonly List<CustomerGridRowItem> _filteredRows = [];

    private readonly AsyncCommand _searchCommand;
    private readonly AsyncCommand _addCustomerCommand;
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;

    private string _searchQuery = string.Empty;
    private string _name = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _address = string.Empty;
    private string _selectedSortColumn = "Name";
    private string _selectedSortDirection = "Ascending";
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _filteredCount;

    public CustomersViewModel()
        : this(
            App.Services.GetRequiredService<ICustomerService>(),
            App.Services.GetRequiredService<IBillingService>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public CustomersViewModel(
        ICustomerService customerService,
        IBillingService billingService,
        IDialogService dialogService)
    {
        _customerService = customerService;
        _billingService = billingService;
        _dialogService = dialogService;

        Customers = [];

        _searchCommand = new AsyncCommand(async () => await RunBusyAsync(SearchInternalAsync));
        _addCustomerCommand = new AsyncCommand(async () => await RunBusyAsync(AddCustomerInternalAsync));
        _previousPageCommand = new RelayCommand(MoveToPreviousPage, () => CanGoPreviousPage);
        _nextPageCommand = new RelayCommand(MoveToNextPage, () => CanGoNextPage);
    }

    public ObservableCollection<CustomerGridRowItem> Customers { get; }

    public IReadOnlyList<string> SortColumns { get; } = ["Account", "Name", "Created"];

    public IReadOnlyList<string> SortDirections { get; } = ["Ascending", "Descending"];

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value))
            {
                return;
            }

            ApplyFilters(resetToFirstPage: true);
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
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

            ApplyFilters(resetToFirstPage: true);
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

            ApplyFilters(resetToFirstPage: true);
        }
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

            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            RaisePropertyChanged(nameof(PageSummary));
            RaisePropertyChanged(nameof(ResultSummary));
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

            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            RaisePropertyChanged(nameof(PageSummary));
            UpdatePagingCommands();
        }
    }

    public bool CanGoPreviousPage => CurrentPage > 1;

    public bool CanGoNextPage => CurrentPage < TotalPages;

    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";

    public string ResultSummary
    {
        get
        {
            if (_filteredCount == 0)
            {
                return "Showing 0 of 0 customers";
            }

            var start = ((CurrentPage - 1) * PageSize) + 1;
            var end = Math.Min(CurrentPage * PageSize, _filteredCount);
            return $"Showing {start}-{end} of {_filteredCount} customers";
        }
    }

    public string FooterCountSummary => _filteredCount == 1 ? "1 result" : $"{_filteredCount} results";

    public bool HasRows => Customers.Count > 0;

    public AsyncCommand SearchCommand => _searchCommand;

    public AsyncCommand AddCustomerCommand => _addCustomerCommand;

    public RelayCommand PreviousPageCommand => _previousPageCommand;

    public RelayCommand NextPageCommand => _nextPageCommand;

    private async Task SearchInternalAsync()
    {
        await LoadCustomersAsync(SearchQuery);
    }

    private async Task AddCustomerInternalAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await _dialogService.AlertAsync("Validation", "Customer name is required.");
            return;
        }

        var customer = new Customer
        {
            Name = Name.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _customerService.CreateCustomerAsync(customer);
        Name = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Address = string.Empty;

        await LoadCustomersAsync(SearchQuery);
        await _dialogService.AlertAsync("Customer", "Customer created successfully.");
    }

    private async Task LoadCustomersAsync(string? query)
    {
        var customers = await _customerService.SearchCustomersAsync(string.IsNullOrWhiteSpace(query) ? null : query);
        var bills = await _billingService.GetBillsAsync();
        var billsByCustomer = bills
            .GroupBy(x => x.CustomerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Bill>)group
                    .OrderByDescending(bill => bill.DueDateUtc)
                    .ThenByDescending(bill => bill.CreatedAtUtc)
                    .ToList());

        _allRows.Clear();
        var rowIndex = 0;
        foreach (var customer in customers.OrderBy(x => x.Name))
        {
            billsByCustomer.TryGetValue(customer.Id, out var customerBills);
            _allRows.Add(MapCustomerRow(customer, customerBills ?? Array.Empty<Bill>(), rowIndex));
            rowIndex++;
        }

        ApplyFilters(resetToFirstPage: true);
        StatusMessage = $"Loaded {_allRows.Count} customer row(s).";
    }

    private void ApplyFilters(bool resetToFirstPage)
    {
        IEnumerable<CustomerGridRowItem> query = _allRows;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var term = SearchQuery.Trim();
            query = query.Where(row =>
                row.AccountNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Address.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.ContactNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.StatusText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedSortColumn switch
        {
            "Account" => query.OrderBy(x => x.AccountNumber),
            "Created" => query.OrderBy(x => x.CreatedAtUtc),
            _ => query.OrderBy(x => x.Name)
        };

        if (string.Equals(SelectedSortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Reverse();
        }

        _filteredRows.Clear();
        _filteredRows.AddRange(query);
        _filteredCount = _filteredRows.Count;

        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        TotalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(1, _filteredCount) / (double)PageSize));
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        RebuildCurrentPageRows();
        RaisePropertyChanged(nameof(ResultSummary));
        RaisePropertyChanged(nameof(FooterCountSummary));
    }

    private void RebuildCurrentPageRows()
    {
        Customers.Clear();
        if (_filteredRows.Count == 0)
        {
            RaisePropertyChanged(nameof(HasRows));
            return;
        }

        var skip = (CurrentPage - 1) * PageSize;
        foreach (var row in _filteredRows.Skip(skip).Take(PageSize))
        {
            Customers.Add(row);
        }

        RaisePropertyChanged(nameof(HasRows));
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

    private static CustomerGridRowItem MapCustomerRow(Customer customer, IReadOnlyList<Bill> bills, int rowIndex)
    {
        var hasOverdue = bills.Any(x => x.Status == BillStatus.Overdue && x.Balance > 0m);
        var hasPending = bills.Any(x => x.Status == BillStatus.Pending && x.Balance > 0m);
        var latestBill = bills.FirstOrDefault();

        var (statusText, statusBackground, statusForeground) = hasOverdue
            ? ("Disconnected", "#FFE8E8", "#C23B3B")
            : hasPending
                ? ("Pending", "#FFF3D9", "#AF6B00")
                : ("Active", "#E8F8EE", "#1F8A57");

        var (applicationText, applicationBackground, applicationForeground) = latestBill switch
        {
            null => ("No data", "#EEF3FB", "#5A6D84"),
            { Status: BillStatus.Overdue } => ("Needs action", "#FFE8E8", "#C23B3B"),
            { Status: BillStatus.Pending } => ("Scheduled", "#EAF0FF", "#2F5DC7"),
            _ => ("Current", "#E8F8EE", "#1F8A57")
        };

        var raw = BitConverter.ToUInt32(customer.Id.ToByteArray(), 0) % 1_000_000;
        var accountNumber = $"22-{raw:000000}-1";

        return new CustomerGridRowItem
        {
            CustomerId = customer.Id,
            AccountNumber = accountNumber,
            Name = customer.Name,
            Address = string.IsNullOrWhiteSpace(customer.Address) ? "Address not set" : customer.Address!,
            ApplicationText = applicationText,
            ApplicationBackground = applicationBackground,
            ApplicationForeground = applicationForeground,
            ContactNumber = string.IsNullOrWhiteSpace(customer.PhoneNumber) ? "--" : customer.PhoneNumber!,
            StatusText = statusText,
            StatusBackground = statusBackground,
            StatusForeground = statusForeground,
            CreatedAtUtc = customer.CreatedAtUtc,
            CreatedDisplay = customer.CreatedAtUtc.ToString("yyyy-MM-dd"),
            RowBackground = rowIndex % 2 == 0 ? "#F9FBFE" : "#F3F7FC"
        };
    }

    private void UpdatePagingCommands()
    {
        _previousPageCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
    }
}
