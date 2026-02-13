using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class ReceiptViewModel(
    IReceiptService receiptService,
    IDialogService dialogService) : BaseViewModel
{
    private string _customerName = string.Empty;
    private string _billNumber = string.Empty;
    private decimal _amount;
    private string _outputPath = string.Empty;

    public ReceiptViewModel() : this(
        App.Services.GetRequiredService<IReceiptService>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string BillNumber
    {
        get => _billNumber;
        set => SetProperty(ref _billNumber, value);
    }

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public AsyncCommand GenerateCommand => new(async () => await RunBusyAsync(async () =>
    {
        var path = await receiptService.GenerateReceiptAsync(new ReceiptDto
        {
            CustomerName = CustomerName,
            BillNumber = BillNumber,
            PaidAmount = Amount,
            PaymentDateUtc = DateTime.UtcNow,
            ReceiptNumber = string.Empty
        });

        OutputPath = path;
        await dialogService.AlertAsync("Receipt", $"Receipt generated at {path}");
    }));
}
