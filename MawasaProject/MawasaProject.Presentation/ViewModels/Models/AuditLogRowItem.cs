using MawasaProject.Domain.Entities;

namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class AuditLogRowItem
{
    public AuditLog Source { get; init; } = new();
    public DateTime TimeUtc { get; init; }
    public string TimeDisplay { get; init; } = string.Empty;
    public string UserDisplay { get; init; } = string.Empty;
    public string ModuleDisplay { get; init; } = string.Empty;
    public string ActionDisplay { get; init; } = string.Empty;
    public string DescriptionDisplay { get; init; } = string.Empty;
    public string RowBackground { get; init; } = "#F9FBFE";
}
