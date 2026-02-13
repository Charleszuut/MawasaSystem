using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Abstractions.Security;

public sealed record SessionContext(
    Guid UserId,
    string Username,
    IReadOnlyCollection<UserRole> Roles,
    DateTime AuthenticatedAtUtc,
    DateTime LastActivityAtUtc);

public interface ISessionService
{
    SessionContext? CurrentSession { get; }
    void Set(SessionContext context);
    void Clear();
    void Touch();
}
