using MawasaProject.Domain.Entities;
using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Abstractions.Services;

public interface IBillingService
{
    Task<Bill> CreateBillAsync(Bill bill, CancellationToken cancellationToken = default);
    Task<Bill?> GetBillByIdAsync(Guid billId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Bill>> GetBillsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillDto>> GetBillsByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task ApplyOverdueAutomationAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task UpdateBillStatusAsync(Guid billId, MawasaProject.Domain.Enums.BillStatus newStatus, Guid changedByUserId, CancellationToken cancellationToken = default);
}
