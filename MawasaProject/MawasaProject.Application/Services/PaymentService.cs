using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Rules;
using MawasaProject.Application.Validation;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class PaymentService(
    IBillRepository billRepository,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IAuditInterceptor auditInterceptor,
    BusinessRuleEngine rules) : IPaymentService
{
    public async Task<Payment> RecordPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        EntityValidator.ValidateObject(payment);

        var bill = await billRepository.GetByIdAsync(payment.BillId, cancellationToken)
            ?? throw new InvalidOperationException("Target bill was not found.");
        rules.EnsurePaymentCanBeApplied(bill, payment.Amount);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var billBefore = new
            {
                bill.Status,
                bill.Balance,
                bill.PaidAtUtc
            };

            var reference = string.IsNullOrWhiteSpace(payment.ReferenceNumber)
                ? $"PMT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                : payment.ReferenceNumber;
            payment.MarkCompleted(reference);
            await paymentRepository.AddAsync(payment, ct);

            var totalPaid = await paymentRepository.GetTotalPaidForBillAsync(payment.BillId, ct);
            bill.RecalculateFromTotalPaid(totalPaid, DateTime.UtcNow);

            await billRepository.UpdateAsync(bill, ct);

            await auditInterceptor.TrackAsync(
                AuditActionType.Create,
                nameof(Payment),
                payment.Id.ToString(),
                oldValue: null,
                newValue: new
                {
                    payment.BillId,
                    payment.Amount,
                    payment.PaymentDateUtc,
                    payment.Status,
                    payment.ReferenceNumber,
                    payment.CreatedByUserId
                },
                context: "Payment recorded",
                username: null,
                ct);

            await auditInterceptor.TrackAsync(
                AuditActionType.Update,
                nameof(Bill),
                bill.Id.ToString(),
                oldValue: billBefore,
                newValue: new
                {
                    bill.Status,
                    bill.Balance,
                    bill.PaidAtUtc
                },
                context: "Bill recalculated after payment",
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
