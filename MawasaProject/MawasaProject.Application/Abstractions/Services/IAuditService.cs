using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Abstractions.Services;

public interface IAuditService
{
    Task LogAsync(AuditActionType action, string entityName, string? entityId, string? oldValuesJson, string? newValuesJson, string? context, string? username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetLogsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
}
