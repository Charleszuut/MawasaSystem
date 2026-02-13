using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using UserRoleType = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Rules;

public sealed class BusinessRuleEngine
{
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
