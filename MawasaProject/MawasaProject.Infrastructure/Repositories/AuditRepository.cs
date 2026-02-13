using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class AuditRepository(ISqliteConnectionManager connectionManager)
    : GenericRepository<AuditLog>(connectionManager), IAuditRepository
{
    protected override string TableName => "AuditLogs";

    protected override string InsertSql =>
        "INSERT INTO AuditLogs (Id, ActionType, EntityName, EntityId, Username, Context, OldValuesJson, NewValuesJson, DeviceIpAddress, TimestampUtc) VALUES ($Id, $ActionType, $EntityName, $EntityId, $Username, $Context, $OldValuesJson, $NewValuesJson, $DeviceIpAddress, $TimestampUtc);";

    protected override string UpdateSql =>
        "UPDATE AuditLogs SET ActionType = $ActionType, EntityName = $EntityName, EntityId = $EntityId, Username = $Username, Context = $Context, OldValuesJson = $OldValuesJson, NewValuesJson = $NewValuesJson, DeviceIpAddress = $DeviceIpAddress, TimestampUtc = $TimestampUtc WHERE Id = $Id;";

    protected override AuditLog Map(SqliteDataReader reader)
    {
        return new AuditLog
        {
            Id = SqliteHelper.GetGuid(reader, "Id"),
            ActionType = (AuditActionType)reader.GetInt32(reader.GetOrdinal("ActionType")),
            EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
            EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId")) ? null : reader.GetString(reader.GetOrdinal("EntityId")),
            Username = reader.IsDBNull(reader.GetOrdinal("Username")) ? null : reader.GetString(reader.GetOrdinal("Username")),
            Context = reader.IsDBNull(reader.GetOrdinal("Context")) ? null : reader.GetString(reader.GetOrdinal("Context")),
            OldValuesJson = reader.IsDBNull(reader.GetOrdinal("OldValuesJson")) ? null : reader.GetString(reader.GetOrdinal("OldValuesJson")),
            NewValuesJson = reader.IsDBNull(reader.GetOrdinal("NewValuesJson")) ? null : reader.GetString(reader.GetOrdinal("NewValuesJson")),
            DeviceIpAddress = reader.IsDBNull(reader.GetOrdinal("DeviceIpAddress")) ? null : reader.GetString(reader.GetOrdinal("DeviceIpAddress")),
            TimestampUtc = SqliteHelper.GetDateTime(reader, "TimestampUtc")
        };
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
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = "SELECT * FROM AuditLogs WHERE ($FromUtc IS NULL OR TimestampUtc >= $FromUtc) AND ($ToUtc IS NULL OR TimestampUtc <= $ToUtc) ORDER BY TimestampUtc DESC;";
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
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }
}
