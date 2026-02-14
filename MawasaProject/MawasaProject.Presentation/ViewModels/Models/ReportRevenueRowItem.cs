namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class ReportRevenueRowItem
{
    public DateTime AnchorDateUtc { get; init; }
    public string Period { get; init; } = string.Empty;
    public int Bills { get; init; }
    public int Paid { get; init; }
    public int Unpaid { get; init; }
    public decimal Revenue { get; init; }
    public string RevenueDisplay => $"P{Revenue:N2}";
    public string RowBackground { get; init; } = "#F9FBFE";
}
