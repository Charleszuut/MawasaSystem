namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class PaymentHistoryRowItem
{
    public DateTime PaymentDateUtc { get; init; }
    public string PaymentDateDisplay { get; init; } = string.Empty;
    public string BillNumber { get; init; } = string.Empty;
    public string ReferenceNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string AmountDisplay { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string StatusBackground { get; init; } = "#EEF3FB";
    public string StatusForeground { get; init; } = "#41556D";
}
