using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Services;

public sealed class DashboardService(IBillRepository billRepository) : IDashboardService
{
    public Task<DashboardSummaryDto> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return billRepository.GetDashboardSummaryAsync(fromUtc, toUtc, cancellationToken);
    }
}
