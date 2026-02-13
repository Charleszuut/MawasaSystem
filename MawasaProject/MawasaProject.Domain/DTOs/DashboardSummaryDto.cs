namespace MawasaProject.Domain.DTOs;

public sealed class DashboardSummaryDto
{
    public decimal TotalRevenue { get; init; }
    public decimal OutstandingBalance { get; init; }
    public int PendingBills { get; init; }
    public int OverdueBills { get; init; }
    public int TotalCustomers { get; init; }
    public IReadOnlyCollection<MonthlyRevenuePointDto> RevenueSeries { get; init; } = Array.Empty<MonthlyRevenuePointDto>();
}

public sealed class MonthlyRevenuePointDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal Revenue { get; init; }
}
