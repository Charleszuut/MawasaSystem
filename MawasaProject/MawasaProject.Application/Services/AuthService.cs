using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Rules;
using MawasaProject.Application.Validation;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ISessionService sessionService,
    IAuditService auditService) : IAuthService
{
    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDeleted || !user.IsActive)
        {
            return new AuthResult(false, "Invalid username or password.");
        }

        if (!passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return new AuthResult(false, "Invalid username or password.");
        }

        var roles = await userRepository.GetUserRolesAsync(user.Id, cancellationToken);
        sessionService.Set(new SessionContext(user.Id, user.Username, roles, DateTime.UtcNow, DateTime.UtcNow));

        user.RegisterLogin();
        await userRepository.UpdateAsync(user, cancellationToken);

        await auditService.LogAsync(
            AuditActionType.Login,
            nameof(User),
            user.Id.ToString(),
            oldValuesJson: null,
            newValuesJson: "{\"status\":\"logged-in\"}",
            context: "Offline local login",
            username: user.Username,
            cancellationToken);

        return new AuthResult(true, "Login successful.", user.Id);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var current = sessionService.CurrentSession;
        if (current is not null)
        {
            await auditService.LogAsync(
                AuditActionType.Logout,
                nameof(User),
                current.UserId.ToString(),
                oldValuesJson: null,
                newValuesJson: "{\"status\":\"logged-out\"}",
                context: "Local logout",
                username: current.Username,
                cancellationToken);
        }

        sessionService.Clear();
    }
}
