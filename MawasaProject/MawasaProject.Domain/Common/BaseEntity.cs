using MawasaProject.Domain.Interfaces;

namespace MawasaProject.Domain.Common;

public abstract class BaseEntity : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public void EnsureValidIdentity()
    {
        DomainGuard.AgainstEmpty(Id, nameof(Id));
    }
}
