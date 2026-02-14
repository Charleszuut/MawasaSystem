using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Services;

public interface IBillingService
{
    Task<Bill> CreateBillAsync(Bill bill, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Bill>> GetBillsAsync(CancellationToken cancellationToken = default);
    Task ApplyOverdueAutomationAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task UpdateBillStatusAsync(Guid billId, MawasaProject.Domain.Enums.BillStatus newStatus, Guid changedByUserId, CancellationToken cancellationToken = default);
}
