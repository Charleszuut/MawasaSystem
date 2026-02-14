using Microsoft.Data.Sqlite;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Data.Mappers;

public static class AuditLogMapper
{
    public static AuditLog FromReader(SqliteDataReader reader)
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
}
