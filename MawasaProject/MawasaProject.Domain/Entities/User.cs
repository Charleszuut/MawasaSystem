using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class User : SoftDeleteEntity
{
    [Required]
    [MaxLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordSalt { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; init; } = new List<UserRole>();

    public void SetCredentials(string hash, string salt)
    {
        DomainGuard.AgainstNullOrWhiteSpace(hash, nameof(hash));
        DomainGuard.AgainstNullOrWhiteSpace(salt, nameof(salt));
        PasswordHash = hash.Trim();
        PasswordSalt = salt.Trim();
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void RegisterLogin(DateTime? loginAtUtc = null)
    {
        LastLoginAtUtc = loginAtUtc ?? DateTime.UtcNow;
        Touch(LastLoginAtUtc);
    }
}
