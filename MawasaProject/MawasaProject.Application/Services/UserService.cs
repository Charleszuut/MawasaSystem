using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Services;

public sealed class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAuditService auditService) : IUserService
{
    public Task<IReadOnlyList<MawasaProject.Domain.DTOs.UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return userRepository.ListUsersWithRolesAsync(cancellationToken);
    }

    public async Task CreateUserAsync(string username, string password, IReadOnlyCollection<UserRole> roles, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var existing = await userRepository.GetByUsernameAsync(username, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var hashed = passwordHasher.Hash(password);

        var user = new User
        {
            Username = username.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        user.SetCredentials(hashed.Hash, hashed.Salt);
        user.Activate();

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.AssignRolesAsync(user.Id, roles, cancellationToken);

        await auditService.LogAsync(
            MawasaProject.Domain.Enums.AuditActionType.Create,
            nameof(User),
            user.Id.ToString(),
            oldValuesJson: null,
            newValuesJson: "{\"username\":\"" + user.Username + "\"}",
            context: "User created",
            username: createdByUserId.ToString(),
            cancellationToken);
    }
}
