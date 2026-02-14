using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Services;

public sealed class DashboardService(IBillRepository billRepository) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        if (fromUtc > toUtc)
        {
            throw new InvalidOperationException("Dashboard start date cannot be later than end date.");
        }

        var summary = await billRepository.GetDashboardSummaryAsync(fromUtc, toUtc, cancellationToken);
        var normalizedSeries = NormalizeRevenueSeries(summary.RevenueSeries, fromUtc, toUtc);

        return new DashboardSummaryDto
        {
            TotalRevenue = summary.TotalRevenue,
            OutstandingBalance = summary.OutstandingBalance,
            TotalBills = summary.TotalBills,
            PendingBills = summary.PendingBills,
            OverdueBills = summary.OverdueBills,
            TotalCustomers = summary.TotalCustomers,
            RevenueSeries = normalizedSeries
        };
    }

    private static IReadOnlyCollection<MonthlyRevenuePointDto> NormalizeRevenueSeries(
        IReadOnlyCollection<MonthlyRevenuePointDto> series,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var startMonth = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = new DateTime(toUtc.Year, toUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var lookup = series.ToDictionary(
            keySelector: p => (p.Year, p.Month),
            elementSelector: p => p.Revenue);

        var output = new List<MonthlyRevenuePointDto>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            output.Add(new MonthlyRevenuePointDto
            {
                Year = month.Year,
                Month = month.Month,
                Revenue = lookup.TryGetValue((month.Year, month.Month), out var revenue) ? revenue : 0m
            });
        }

        return output;
    }
}
