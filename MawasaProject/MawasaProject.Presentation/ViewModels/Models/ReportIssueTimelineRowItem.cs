namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class ReportIssueTimelineRowItem
{
    public DateTime DateUtc { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public int IssueCount { get; init; }
    public string RowBackground { get; init; } = "#F9FBFE";
}
