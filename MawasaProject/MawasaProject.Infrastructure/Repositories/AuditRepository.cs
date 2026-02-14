using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.Mappers;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class AuditRepository(
    ISqliteConnectionManager connectionManager,
    SqliteDatabaseOptions options,
    IAppLogger<AuditRepository> logger)
    : GenericRepository<AuditLog>(connectionManager, options), IAuditRepository
{
    protected override string TableName => "AuditLogs";

    protected override string InsertSql =>
        "INSERT INTO AuditLogs (Id, ActionType, EntityName, EntityId, Username, Context, OldValuesJson, NewValuesJson, DeviceIpAddress, TimestampUtc) VALUES ($Id, $ActionType, $EntityName, $EntityId, $Username, $Context, $OldValuesJson, $NewValuesJson, $DeviceIpAddress, $TimestampUtc);";

    protected override string UpdateSql =>
        "UPDATE AuditLogs SET ActionType = $ActionType, EntityName = $EntityName, EntityId = $EntityId, Username = $Username, Context = $Context, OldValuesJson = $OldValuesJson, NewValuesJson = $NewValuesJson, DeviceIpAddress = $DeviceIpAddress, TimestampUtc = $TimestampUtc WHERE Id = $Id;";

    protected override AuditLog Map(SqliteDataReader reader)
    {
        return AuditLogMapper.FromReader(reader);
    }

    protected override void BindInsert(SqliteCommand command, AuditLog entity)
    {
        command.Parameters.AddWithValue("$Id", entity.Id.ToString());
        command.Parameters.AddWithValue("$ActionType", (int)entity.ActionType);
        command.Parameters.AddWithValue("$EntityName", entity.EntityName);
        command.Parameters.AddWithValue("$EntityId", (object?)entity.EntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$Username", (object?)entity.Username ?? DBNull.Value);
        command.Parameters.AddWithValue("$Context", (object?)entity.Context ?? DBNull.Value);
        command.Parameters.AddWithValue("$OldValuesJson", (object?)entity.OldValuesJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$NewValuesJson", (object?)entity.NewValuesJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$DeviceIpAddress", (object?)entity.DeviceIpAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("$TimestampUtc", entity.TimestampUtc.ToString("O"));
    }

    protected override void BindUpdate(SqliteCommand command, AuditLog entity)
    {
        BindInsert(command, entity);
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = CreateCommand(connection, "SELECT * FROM AuditLogs WHERE ($FromUtc IS NULL OR TimestampUtc >= $FromUtc) AND ($ToUtc IS NULL OR TimestampUtc <= $ToUtc) ORDER BY TimestampUtc DESC;");
            command.Parameters.AddWithValue("$FromUtc", (object?)fromUtc?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$ToUtc", (object?)toUtc?.ToString("O") ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<AuditLog>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(Map(reader));
            }

            return output;
        }
        catch (SqliteException exception)
        {
            logger.Error(
                exception,
                "Failed to query audit logs from {0} to {1}",
                fromUtc?.ToString("O") ?? "null",
                toUtc?.ToString("O") ?? "null");
            throw CreateRepositoryException("Query", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }
}
