using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class AuditService(IAuditRepository auditRepository) : IAuditService
{
    public async Task LogAsync(
        AuditActionType action,
        string entityName,
        string? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? context,
        string? username,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            ActionType = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            Context = context,
            Username = username,
            TimestampUtc = DateTime.UtcNow,
            DeviceIpAddress = "offline"
        };

        await auditRepository.AddAsync(log, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLog>> GetLogsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        return auditRepository.QueryAsync(fromUtc, toUtc, cancellationToken);
    }
}
