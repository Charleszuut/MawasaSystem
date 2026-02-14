namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class DashboardRevenuePoint
{
    public required string Label { get; init; }
    public decimal Revenue { get; init; }
    public double RelativeHeight { get; init; }
}
