using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Domain.Entities;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class BillingViewModel(
    IBillingService billingService,
    AppStateStore stateStore,
    IDialogService dialogService) : BaseViewModel
{
    private string _billNumber = string.Empty;
    private string _customerIdText = string.Empty;
    private decimal _amount;
    private DateTime _dueDateUtc = DateTime.UtcNow.AddDays(14);

    public BillingViewModel() : this(
        App.Services.GetRequiredService<IBillingService>(),
        App.Services.GetRequiredService<AppStateStore>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

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

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public DateTime DueDateUtc
    {
        get => _dueDateUtc;
        set => SetProperty(ref _dueDateUtc, value);
    }

    public AsyncCommand CreateBillCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (!Guid.TryParse(CustomerIdText, out var customerId))
        {
            await dialogService.AlertAsync("Validation", "Customer ID must be a valid GUID.");
            return;
        }

        var userId = stateStore.Session?.UserId ?? Guid.Empty;

        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            BillNumber = BillNumber,
            CustomerId = customerId,
            Amount = Amount,
            Balance = Amount,
            DueDateUtc = DueDateUtc,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await billingService.CreateBillAsync(bill);
        await dialogService.AlertAsync("Billing", "Bill created successfully.");
    }));
}
