using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.DTOs;

public sealed class UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public IReadOnlyCollection<UserRole> Roles { get; init; } = Array.Empty<UserRole>();
}
