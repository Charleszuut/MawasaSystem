using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class InvoiceRecord : AuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid BillId { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
