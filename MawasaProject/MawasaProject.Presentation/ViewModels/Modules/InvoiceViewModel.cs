using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.Validation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class InvoiceViewModel : BaseViewModel
{
    private readonly IInvoiceService _invoiceService;
    private readonly IDialogService _dialogService;

    private string _customerName = string.Empty;
    private string _billNumber = string.Empty;
    private string _billIdText = string.Empty;
    private decimal _totalAmount;
    private DateTime _dueDateUtc = DateTime.UtcNow.AddDays(30);
    private string _selectedTemplate = "standard";
    private string _qrPayload = string.Empty;
    private bool _autoPrint = true;

    private string _outputPdfPath = string.Empty;
    private string _outputLayoutPath = string.Empty;
    private string _outputImagePath = string.Empty;
    private string _outputCsvPath = string.Empty;

    public InvoiceViewModel()
        : this(
            App.Services.GetRequiredService<IInvoiceService>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public InvoiceViewModel(IInvoiceService invoiceService, IDialogService dialogService)
    {
        _invoiceService = invoiceService;
        _dialogService = dialogService;

        Templates.Add("standard");
        Templates.Add("detailed");
    }

    public ObservableCollection<string> Templates { get; } = [];

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

    public string BillIdText
    {
        get => _billIdText;
        set => SetProperty(ref _billIdText, value);
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

    public string SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string QrPayload
    {
        get => _qrPayload;
        set => SetProperty(ref _qrPayload, value);
    }

    public bool AutoPrint
    {
        get => _autoPrint;
        set => SetProperty(ref _autoPrint, value);
    }

    public string OutputPdfPath
    {
        get => _outputPdfPath;
        set => SetProperty(ref _outputPdfPath, value);
    }

    public string OutputLayoutPath
    {
        get => _outputLayoutPath;
        set => SetProperty(ref _outputLayoutPath, value);
    }

    public string OutputImagePath
    {
        get => _outputImagePath;
        set => SetProperty(ref _outputImagePath, value);
    }

    public string OutputCsvPath
    {
        get => _outputCsvPath;
        set => SetProperty(ref _outputCsvPath, value);
    }

    public AsyncCommand GenerateCommand => new(async () => await RunBusyAsync(async () =>
    {
        var validations = ValidationFramework.Combine(
            ValidationFramework.ValidateRequired(nameof(CustomerName), CustomerName, "Customer name is required."),
            ValidationFramework.ValidateRequired(nameof(BillNumber), BillNumber, "Bill number is required."),
            ValidationFramework.ValidatePositiveAmount(nameof(TotalAmount), TotalAmount, "Total amount must be greater than zero."));
        SetValidationErrors(validations);
        if (validations.Count > 0)
        {
            await _dialogService.AlertAsync("Validation", string.Join("\n", validations.Select(x => x.Message)));
            return;
        }

        Guid? billId = null;
        if (!string.IsNullOrWhiteSpace(BillIdText))
        {
            if (!Guid.TryParse(BillIdText.Trim(), out var parsed))
            {
                await _dialogService.AlertAsync("Validation", "Bill ID must be a valid GUID.");
                return;
            }

            billId = parsed;
        }

        var result = await _invoiceService.GenerateInvoiceAsync(new InvoiceDto
        {
            InvoiceNumber = string.Empty,
            BillId = billId,
            BillNumber = BillNumber.Trim(),
            CustomerName = CustomerName.Trim(),
            TotalAmount = TotalAmount,
            DueDateUtc = DueDateUtc,
            TemplateName = SelectedTemplate,
            QrPayload = string.IsNullOrWhiteSpace(QrPayload) ? null : QrPayload.Trim(),
            AutoPrint = AutoPrint
        });

        OutputPdfPath = result.PdfPath;
        OutputLayoutPath = result.LayoutPath;
        OutputImagePath = result.ImagePath;
        OutputCsvPath = result.CsvReferencePath;
        StatusMessage = $"Invoice generated: {result.DocumentNumber}";

        await _dialogService.AlertAsync("Invoice", $"Invoice generated:\n{result.PdfPath}");
    }));
}
