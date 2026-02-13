using MawasaProject.Domain.Interfaces;

namespace MawasaProject.Domain.Common;

public abstract class SoftDeleteEntity : AuditableEntity, ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public void MarkDeleted(DateTime? deletedAtUtc = null)
    {
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc ?? DateTime.UtcNow;
        Touch(DeletedAtUtc);
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        Touch();
    }
}
