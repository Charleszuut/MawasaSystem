using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class InvoiceViewModel(
    IInvoiceService invoiceService,
    IDialogService dialogService) : BaseViewModel
{
    private string _customerName = string.Empty;
    private string _billNumber = string.Empty;
    private decimal _totalAmount;
    private DateTime _dueDateUtc = DateTime.UtcNow.AddDays(30);
    private string _outputPath = string.Empty;

    public InvoiceViewModel() : this(
        App.Services.GetRequiredService<IInvoiceService>(),
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

    public decimal TotalAmount
    {
        get => _totalAmount;
        set => SetProperty(ref _totalAmount, value);
    }

    public DateTime DueDateUtc
    {
        get => _dueDateUtc;
        set => SetProperty(ref _dueDateUtc, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public AsyncCommand GenerateCommand => new(async () => await RunBusyAsync(async () =>
    {
        var path = await invoiceService.GenerateInvoiceAsync(new InvoiceDto
        {
            CustomerName = CustomerName,
            BillNumber = BillNumber,
            TotalAmount = TotalAmount,
            DueDateUtc = DueDateUtc,
            InvoiceNumber = string.Empty
        });

        OutputPath = path;
        await dialogService.AlertAsync("Invoice", $"Invoice generated at {path}");
    }));
}
