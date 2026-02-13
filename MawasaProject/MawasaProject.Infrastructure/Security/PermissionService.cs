using MawasaProject.Application.Abstractions.Security;

namespace MawasaProject.Infrastructure.Security;

public sealed class PermissionService(IRbacService rbacService)
{
    public bool Can(SessionContext? session, string permission)
    {
        return rbacService.HasPermission(session, permission);
    }
}
