using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Services;

public sealed class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IAuditInterceptor auditInterceptor) : IUserService
{
    public Task<IReadOnlyList<MawasaProject.Domain.DTOs.UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return userRepository.ListUsersWithRolesAsync(cancellationToken);
    }

    public async Task CreateUserAsync(string username, string password, IReadOnlyCollection<UserRole> roles, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters long.");
        }

        if (roles is null || roles.Count == 0)
        {
            throw new InvalidOperationException("At least one user role is required.");
        }

        var normalizedUsername = username.Trim();
        var existing = await userRepository.GetByUsernameAsync(normalizedUsername, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var hashed = passwordHasher.Hash(password);

            var user = new User
            {
                Username = normalizedUsername,
                CreatedAtUtc = DateTime.UtcNow
            };
            user.SetCredentials(hashed.Hash, hashed.Salt);
            user.Activate();

            await userRepository.AddAsync(user, ct);
            await userRepository.AssignRolesAsync(user.Id, roles, ct);

            await auditInterceptor.TrackAsync(
                MawasaProject.Domain.Enums.AuditActionType.Create,
                nameof(User),
                user.Id.ToString(),
                oldValue: null,
                newValue: new
                {
                    user.Username,
                    user.IsActive,
                    Roles = roles,
                    CreatedByUserId = createdByUserId
                },
                context: "User created",
                username: createdByUserId.ToString(),
                ct);
        }, cancellationToken);
    }
}
