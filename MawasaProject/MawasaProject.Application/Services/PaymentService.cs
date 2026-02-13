using System.Text.Json;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Validation;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class PaymentService(
    IBillRepository billRepository,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService) : IPaymentService
{
    public async Task<Payment> RecordPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        EntityValidator.ValidateObject(payment);

        var bill = await billRepository.GetByIdAsync(payment.BillId, cancellationToken)
            ?? throw new InvalidOperationException("Target bill was not found.");

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var reference = string.IsNullOrWhiteSpace(payment.ReferenceNumber)
                ? $"PMT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                : payment.ReferenceNumber;
            payment.MarkCompleted(reference);
            await paymentRepository.AddAsync(payment, ct);

            var totalPaid = await paymentRepository.GetTotalPaidForBillAsync(payment.BillId, ct);
            bill.RecalculateFromTotalPaid(totalPaid, DateTime.UtcNow);

            await billRepository.UpdateAsync(bill, ct);

            await auditService.LogAsync(
                AuditActionType.Update,
                nameof(Payment),
                payment.Id.ToString(),
                oldValuesJson: null,
                newValuesJson: JsonSerializer.Serialize(payment),
                context: "Payment recorded",
                username: null,
                ct);
        }, cancellationToken);

        return payment;
    }

    public Task<IReadOnlyList<Payment>> GetPaymentsByBillAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        return paymentRepository.GetByBillIdAsync(billId, cancellationToken);
    }
}
