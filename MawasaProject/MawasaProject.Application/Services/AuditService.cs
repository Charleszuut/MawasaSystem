using System.Text.Json;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class AuditService(
    IAuditRepository auditRepository,
    ISessionService sessionService,
    IAuditContextProvider contextProvider) : IAuditService
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
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new InvalidOperationException("Audit entity name is required.");
        }

        var session = sessionService.CurrentSession;
        var runtime = contextProvider.Capture();
        var resolvedUsername = string.IsNullOrWhiteSpace(username) ? session?.Username : username;

        var log = new AuditLog
        {
            ActionType = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            Context = BuildContext(context, runtime, session),
            Username = resolvedUsername,
            TimestampUtc = DateTime.UtcNow,
            DeviceIpAddress = runtime.DeviceIpAddress
        };

        await auditRepository.AddAsync(log, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLog>> GetLogsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        return auditRepository.QueryAsync(fromUtc, toUtc, cancellationToken);
    }

    private static string BuildContext(string? context, AuditContextSnapshot runtime, SessionContext? session)
    {
        var boundedContext = context;
        if (!string.IsNullOrWhiteSpace(boundedContext) && boundedContext.Length > 180)
        {
            boundedContext = boundedContext[..180];
        }

        var payload = new
        {
            Context = boundedContext,
            Device = runtime.DeviceName,
            Os = runtime.OsDescription,
            AppVersion = runtime.AppVersion,
            SessionUserId = session?.UserId,
            SessionAuthenticatedAtUtc = session?.AuthenticatedAtUtc,
            SessionLastActivityAtUtc = session?.LastActivityAtUtc
        };

        var json = JsonSerializer.Serialize(payload);
        if (json.Length <= 500)
        {
            return json;
        }

        var fallback = $"Context={boundedContext};Device={runtime.DeviceName};Os={runtime.OsDescription};AppVersion={runtime.AppVersion};SessionUserId={session?.UserId}";
        return fallback.Length <= 500 ? fallback : fallback[..500];
    }
}
