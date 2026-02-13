using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Persistence;

public interface IAuditRepository : IRepository<AuditLog>
{
    Task<IReadOnlyList<AuditLog>> QueryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
}
