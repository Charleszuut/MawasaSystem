using MawasaProject.Domain.Interfaces;

namespace MawasaProject.Domain.Common;

public abstract class AuditableEntity : BaseEntity, IAuditable
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public void Touch(DateTime? updatedAtUtc = null)
    {
        UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow;
    }
}
