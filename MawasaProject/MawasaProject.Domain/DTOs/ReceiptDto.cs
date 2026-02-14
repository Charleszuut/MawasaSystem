namespace MawasaProject.Domain.DTOs;

public sealed class ReceiptDto
{
    public string ReceiptNumber { get; init; } = string.Empty;
    public Guid? PaymentId { get; init; }
    public string BillNumber { get; init; } = string.Empty;
    public decimal PaidAmount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string TemplateName { get; init; } = "standard";
    public string Branding { get; init; } = "Mawasa Project";
    public string? QrPayload { get; init; }
    public bool AutoPrint { get; init; } = true;
}
