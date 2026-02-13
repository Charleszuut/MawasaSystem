using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Abstractions.Security;

public interface IRbacService
{
    bool HasRole(SessionContext? session, UserRole role);
    bool HasPermission(SessionContext? session, string permission);
    IReadOnlyCollection<string> GetPermissionsForRoles(IReadOnlyCollection<UserRole> roles);
}
