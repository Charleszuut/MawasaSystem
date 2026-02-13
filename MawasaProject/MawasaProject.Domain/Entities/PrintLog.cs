using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class PrintLog : AuditableEntity
{
    public Guid PrintJobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime LoggedAtUtc { get; set; } = DateTime.UtcNow;
}
