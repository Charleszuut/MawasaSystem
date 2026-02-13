using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.Entities;

public sealed class BillStatusHistory : AuditableEntity
{
    [Required]
    public Guid BillId { get; set; }

    public BillStatus OldStatus { get; set; }

    public BillStatus NewStatus { get; set; }

    public Guid ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
