using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class ReceiptRecord : AuditableEntity
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
