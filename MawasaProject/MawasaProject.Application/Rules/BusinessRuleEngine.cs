using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using UserRoleType = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Rules;

public sealed class BusinessRuleEngine
{
    public void EnsurePaymentCanBeApplied(Bill bill, decimal paymentAmount)
    {
        if (bill.IsDeleted)
        {
            throw new InvalidOperationException("Cannot record payment for a deleted bill.");
        }

        if (paymentAmount <= 0m)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        if (bill.Balance <= 0m || bill.Status == BillStatus.Paid)
        {
            throw new InvalidOperationException("Bill is already fully paid.");
        }

        if (paymentAmount > bill.Balance)
        {
            throw new InvalidOperationException("Payment amount exceeds current bill balance.");
        }
    }

    public void EnsureBillStatusTransitionAllowed(Bill bill, BillStatus targetStatus)
    {
        if (bill.IsDeleted)
        {
            throw new InvalidOperationException("Cannot update status for a deleted bill.");
        }

        if (bill.Status == BillStatus.Paid && bill.Balance == 0m && targetStatus != BillStatus.Paid)
        {
            throw new InvalidOperationException("Cannot move a fully paid bill back to pending or overdue.");
        }
    }

    public BillStatus ResolveBillStatus(Bill bill, decimal totalPaid, DateTime nowUtc)
    {
        if (totalPaid >= bill.Amount)
        {
            return BillStatus.Paid;
        }

        if (bill.DueDateUtc < nowUtc)
        {
            return BillStatus.Overdue;
        }

        return BillStatus.Pending;
    }

    public bool CanRestoreBackup(IReadOnlyCollection<UserRoleType> roles)
    {
        return roles.Contains(UserRoleType.Admin);
    }
}
