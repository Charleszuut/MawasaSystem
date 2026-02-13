using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class UserRole : AuditableEntity
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid RoleId { get; set; }
}
