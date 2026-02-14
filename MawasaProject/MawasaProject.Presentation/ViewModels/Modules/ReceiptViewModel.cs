using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.Validation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class ReceiptViewModel : BaseViewModel
{
    private readonly IReceiptService _receiptService;
    private readonly IDialogService _dialogService;

    private string _customerName = string.Empty;
    private string _billNumber = string.Empty;
    private string _paymentIdText = string.Empty;
    private decimal _amount;
    private string _selectedTemplate = "standard";
    private string _qrPayload = string.Empty;
    private bool _autoPrint = true;

    private string _outputPdfPath = string.Empty;
    private string _outputLayoutPath = string.Empty;
    private string _outputImagePath = string.Empty;
    private string _outputCsvPath = string.Empty;

    public ReceiptViewModel()
        : this(
            App.Services.GetRequiredService<IReceiptService>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public ReceiptViewModel(IReceiptService receiptService, IDialogService dialogService)
    {
        _receiptService = receiptService;
        _dialogService = dialogService;

        Templates.Add("standard");
        Templates.Add("compact");
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

    public string PaymentIdText
    {
        get => _paymentIdText;
        set => SetProperty(ref _paymentIdText, value);
    }

    public decimal Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
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
            ValidationFramework.ValidatePositiveAmount(nameof(Amount), Amount, "Amount must be greater than zero."));
        SetValidationErrors(validations);
        if (validations.Count > 0)
        {
            await _dialogService.AlertAsync("Validation", string.Join("\n", validations.Select(x => x.Message)));
            return;
        }

        Guid? paymentId = null;
        if (!string.IsNullOrWhiteSpace(PaymentIdText))
        {
            if (!Guid.TryParse(PaymentIdText.Trim(), out var parsed))
            {
                await _dialogService.AlertAsync("Validation", "Payment ID must be a valid GUID.");
                return;
            }

            paymentId = parsed;
        }

        var result = await _receiptService.GenerateReceiptAsync(new ReceiptDto
        {
            CustomerName = CustomerName.Trim(),
            BillNumber = BillNumber.Trim(),
            PaidAmount = Amount,
            PaymentDateUtc = DateTime.UtcNow,
            ReceiptNumber = string.Empty,
            PaymentId = paymentId,
            TemplateName = SelectedTemplate,
            QrPayload = string.IsNullOrWhiteSpace(QrPayload) ? null : QrPayload.Trim(),
            AutoPrint = AutoPrint
        });

        OutputPdfPath = result.PdfPath;
        OutputLayoutPath = result.LayoutPath;
        OutputImagePath = result.ImagePath;
        OutputCsvPath = result.CsvReferencePath;
        StatusMessage = $"Receipt generated: {result.DocumentNumber}";

        await _dialogService.AlertAsync("Receipt", $"Receipt generated:\n{result.PdfPath}");
    }));
}
