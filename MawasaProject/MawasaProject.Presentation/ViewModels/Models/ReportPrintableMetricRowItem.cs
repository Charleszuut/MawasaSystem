namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class ReportPrintableMetricRowItem
{
    public string Metric { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string RowBackground { get; init; } = "#F9FBFE";
}
