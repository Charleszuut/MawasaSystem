using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Infrastructure.Services.Audit;

public sealed class AuditInterceptor(IAuditService auditService, EntityDiffService diffService) : IAuditInterceptor
{
    public Task TrackAsync(
        AuditActionType action,
        string entityName,
        string? entityId,
        object? oldValue,
        object? newValue,
        string? context,
        string? username,
        CancellationToken cancellationToken = default)
    {
        var (oldJson, newJson) = diffService.Diff(oldValue, newValue);

        return auditService.LogAsync(
            action,
            entityName,
            entityId,
            oldJson,
            newJson,
            context,
            username,
            cancellationToken);
    }
}
