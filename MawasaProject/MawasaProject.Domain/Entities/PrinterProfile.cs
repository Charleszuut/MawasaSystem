using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class PrinterProfile : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string PaperSize { get; set; } = "A4";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
