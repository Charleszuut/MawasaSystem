using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.Entities;

public sealed class AuditLog : BaseEntity
{
    public AuditActionType ActionType { get; set; }

    [Required]
    [MaxLength(80)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityId { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    [MaxLength(500)]
    public string? Context { get; set; }

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    [MaxLength(45)]
    public string? DeviceIpAddress { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
