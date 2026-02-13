using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Abstractions.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
