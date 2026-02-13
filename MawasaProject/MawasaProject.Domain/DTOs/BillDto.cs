using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.DTOs;

public sealed class BillDto
{
    public Guid Id { get; init; }
    public string BillNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Balance { get; init; }
    public DateTime DueDateUtc { get; init; }
    public BillStatus Status { get; init; }
}
