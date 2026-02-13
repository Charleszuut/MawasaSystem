using MawasaProject.Application.Abstractions.Security;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Infrastructure.Security;

public sealed class RoleBasedAccessService : IRbacService
{
    private static readonly IReadOnlyDictionary<UserRole, IReadOnlyCollection<string>> RolePermissions =
        new Dictionary<UserRole, IReadOnlyCollection<string>>
        {
            [UserRole.Admin] =
            [
                "users.manage",
                "customers.manage",
                "billing.manage",
                "payments.manage",
                "reports.export",
                "audit.view",
                "backup.create",
                "backup.restore",
                "printer.manage",
                "documents.generate"
            ],
            [UserRole.Staff] =
            [
                "customers.manage",
                "billing.manage",
                "payments.manage",
                "reports.export",
                "documents.generate"
            ]
        };

    public bool HasRole(SessionContext? session, UserRole role)
    {
        return session is not null && session.Roles.Contains(role);
    }

    public bool HasPermission(SessionContext? session, string permission)
    {
        if (session is null)
        {
            return false;
        }

        var granted = GetPermissionsForRoles(session.Roles);
        return granted.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> GetPermissionsForRoles(IReadOnlyCollection<UserRole> roles)
    {
        var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            if (!RolePermissions.TryGetValue(role, out var permissions))
            {
                continue;
            }

            foreach (var permission in permissions)
            {
                output.Add(permission);
            }
        }

        return output.ToArray();
    }
}
