using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Abstractions.Persistence;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRole>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AssignRolesAsync(Guid userId, IReadOnlyCollection<UserRole> roles, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default);
}
