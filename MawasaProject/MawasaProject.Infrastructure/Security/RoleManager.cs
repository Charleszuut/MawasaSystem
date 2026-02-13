using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Infrastructure.Security;

public sealed class RoleManager
{
    public IReadOnlyCollection<UserRole> GetDefaultRoles() => [UserRole.Staff];

    public IReadOnlyCollection<UserRole> ForUserType(bool isAdmin)
    {
        return isAdmin ? [UserRole.Admin] : [UserRole.Staff];
    }
}
