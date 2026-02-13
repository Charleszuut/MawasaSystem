namespace MawasaProject.Domain.DTOs;

public sealed class ReceiptDto
{
    public string ReceiptNumber { get; init; } = string.Empty;
    public string BillNumber { get; init; } = string.Empty;
    public decimal PaidAmount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public string CustomerName { get; init; } = string.Empty;
}
