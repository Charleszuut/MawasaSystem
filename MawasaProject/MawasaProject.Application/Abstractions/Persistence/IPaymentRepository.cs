using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Persistence;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IReadOnlyList<Payment>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalPaidForBillAsync(Guid billId, CancellationToken cancellationToken = default);
}
