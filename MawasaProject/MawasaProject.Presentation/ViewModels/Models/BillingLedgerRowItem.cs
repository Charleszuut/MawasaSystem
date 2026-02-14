namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class BillingLedgerRowItem
{
    public Guid BillId { get; init; }
    public string BillNumber { get; init; } = string.Empty;
    public string CustomerDisplayName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string BillingPeriod { get; init; } = string.Empty;
    public DateTime DueDateUtc { get; init; }
    public string DueDateDisplay { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Balance { get; init; }
    public string BillStatusText { get; init; } = string.Empty;
    public string BillStatusBackground { get; init; } = "#FFEAD5";
    public string BillStatusForeground { get; init; } = "#B85A00";
    public string PrintStatusText { get; init; } = string.Empty;
    public string PrintStatusBackground { get; init; } = "#E8F7EF";
    public string PrintStatusForeground { get; init; } = "#18794E";
}
