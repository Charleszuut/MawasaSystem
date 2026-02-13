using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class Role : SoftDeleteEntity
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; init; } = new List<UserRole>();
}
