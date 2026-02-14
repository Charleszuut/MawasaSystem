using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Abstractions.Services;

public interface IAuditInterceptor
{
    Task TrackAsync(
        AuditActionType action,
        string entityName,
        string? entityId,
        object? oldValue,
        object? newValue,
        string? context,
        string? username,
        CancellationToken cancellationToken = default);
}
