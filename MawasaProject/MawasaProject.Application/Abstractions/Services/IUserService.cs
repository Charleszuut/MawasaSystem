using MawasaProject.Domain.DTOs;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Application.Abstractions.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task CreateUserAsync(string username, string password, IReadOnlyCollection<UserRole> roles, Guid createdByUserId, CancellationToken cancellationToken = default);
}
