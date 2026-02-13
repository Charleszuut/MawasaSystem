using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class PaymentsViewModel(
    IPaymentService paymentService,
    IDialogService dialogService,
    AppStateStore stateStore) : BaseViewModel
{
    private string _billIdText = string.Empty;
    private decimal _amount;
    private string _reference = string.Empty;

    public PaymentsViewModel() : this(
        App.Services.GetRequiredService<IPaymentService>(),
        App.Services.GetRequiredService<IDialogService>(),
        App.Services.GetRequiredService<AppStateStore>())
    {
    }

    public ObservableCollection<Payment> Payments { get; } = [];

    public string BillIdText
    {
        get => _billIdText;
        set => SetProperty(ref _billIdText, value);
    }

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public string Reference
    {
        get => _reference;
        set => SetProperty(ref _reference, value);
    }

    public AsyncCommand RecordPaymentCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (!Guid.TryParse(BillIdText, out var billId))
        {
            await dialogService.AlertAsync("Validation", "Bill ID must be a valid GUID.");
            return;
        }

        var payment = new Payment
        {
            BillId = billId,
            Amount = Amount,
            ReferenceNumber = Reference,
            CreatedByUserId = stateStore.Session?.UserId ?? Guid.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };

        var saved = await paymentService.RecordPaymentAsync(payment);
        Payments.Insert(0, saved);
        await dialogService.AlertAsync("Payment", "Payment recorded.");
    }));

    public AsyncCommand LoadPaymentsCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (!Guid.TryParse(BillIdText, out var billId))
        {
            return;
        }

        var items = await paymentService.GetPaymentsByBillAsync(billId);
        Payments.Clear();
        foreach (var item in items)
        {
            Payments.Add(item);
        }
    }));
}
