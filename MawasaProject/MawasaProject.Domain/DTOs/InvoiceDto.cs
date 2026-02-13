namespace MawasaProject.Domain.DTOs;

public sealed class InvoiceDto
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public string BillNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime DueDateUtc { get; init; }
}
