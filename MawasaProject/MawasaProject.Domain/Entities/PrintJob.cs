using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class PrintJob : AuditableEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string Status { get; set; } = "Queued";
}
