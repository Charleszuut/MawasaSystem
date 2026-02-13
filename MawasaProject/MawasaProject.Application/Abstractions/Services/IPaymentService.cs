using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Services;

public interface IPaymentService
{
    Task<Payment> RecordPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetPaymentsByBillAsync(Guid billId, CancellationToken cancellationToken = default);
}
