using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Rules;
using MawasaProject.Application.Validation;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class BillingService(
    IBillRepository billRepository,
    IUnitOfWork unitOfWork,
    IAuditInterceptor auditInterceptor,
    BusinessRuleEngine rules) : IBillingService
{
    public Task<IReadOnlyList<Bill>> GetBillsAsync(CancellationToken cancellationToken = default)
    {
        return billRepository.ListAsync(cancellationToken);
    }

    public async Task<Bill> CreateBillAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        EntityValidator.ValidateObject(bill);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            bill.InitializeForCreate();
            var resolved = rules.ResolveBillStatus(bill, totalPaid: 0m, DateTime.UtcNow);
            if (resolved == BillStatus.Overdue)
            {
                bill.MarkOverdue();
            }
            await billRepository.AddAsync(bill, ct);

            await auditInterceptor.TrackAsync(
                AuditActionType.Create,
                nameof(Bill),
                bill.Id.ToString(),
                oldValue: null,
                newValue: new
                {
                    bill.BillNumber,
                    bill.CustomerId,
                    bill.Amount,
                    bill.Balance,
                    bill.DueDateUtc,
                    bill.Status,
                    bill.CreatedByUserId
                },
                context: "Bill created",
                username: null,
                ct);
        }, cancellationToken);

        return bill;
    }

    public async Task ApplyOverdueAutomationAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var overdue = await billRepository.GetOverdueBillsAsync(asOfUtc, cancellationToken);
        foreach (var bill in overdue)
        {
            if (bill.Status != BillStatus.Overdue)
            {
                await UpdateBillStatusAsync(bill.Id, BillStatus.Overdue, bill.CreatedByUserId, cancellationToken);
            }
        }
    }

    public async Task UpdateBillStatusAsync(Guid billId, BillStatus newStatus, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var bill = await billRepository.GetByIdAsync(billId, ct)
                ?? throw new InvalidOperationException("Bill was not found.");

            rules.EnsureBillStatusTransitionAllowed(bill, newStatus);
            var oldState = new
            {
                bill.Status,
                bill.Balance,
                bill.PaidAtUtc
            };

            switch (newStatus)
            {
                case BillStatus.Paid:
                    bill.MarkPaid();
                    break;
                case BillStatus.Overdue:
                    bill.MarkOverdue();
                    break;
                default:
                    bill.MarkPending();
                    break;
            }

            await billRepository.UpdateAsync(bill, ct);
            await auditInterceptor.TrackAsync(
                AuditActionType.Update,
                nameof(Bill),
                bill.Id.ToString(),
                oldValue: oldState,
                newValue: new
                {
                    bill.Status,
                    bill.Balance,
                    bill.PaidAtUtc,
                    ChangedBy = changedByUserId
                },
                context: "Bill status updated",
                username: null,
                ct);
        }, cancellationToken);
    }
}
