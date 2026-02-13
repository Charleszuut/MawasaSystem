using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Services;

public sealed record AuthResult(bool Success, string Message, Guid? UserId = null);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
