namespace MawasaProject.Domain.DTOs;

public sealed class InvoiceDto
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public Guid? BillId { get; init; }
    public string BillNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime DueDateUtc { get; init; }
    public string TemplateName { get; init; } = "standard";
    public string Branding { get; init; } = "Mawasa Project";
    public string? QrPayload { get; init; }
    public bool AutoPrint { get; init; } = true;
}
