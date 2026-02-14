using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class PrintJob : AuditableEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public string PrinterName { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public string PaperSize { get; set; } = "A4";
    public string Payload { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? LastError { get; set; }
    public DateTime? LastTriedAtUtc { get; set; }
    public string Status { get; set; } = "Queued";
}
