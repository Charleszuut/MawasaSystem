using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Persistence;

public interface IBillRepository : IRepository<Bill>
{
    Task<IReadOnlyList<Bill>> GetOverdueBillsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillDto>> GetBillsByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
