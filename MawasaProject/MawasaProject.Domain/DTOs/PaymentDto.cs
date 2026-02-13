using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.DTOs;

public sealed class PaymentDto
{
    public Guid Id { get; init; }
    public Guid BillId { get; init; }
    public decimal Amount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public PaymentStatus Status { get; init; }
    public string? ReferenceNumber { get; init; }
}
