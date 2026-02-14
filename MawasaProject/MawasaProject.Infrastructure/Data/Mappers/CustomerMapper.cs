using Microsoft.Data.Sqlite;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Data.Mappers;

public static class CustomerMapper
{
    public static Customer FromReader(SqliteDataReader reader)
    {
        return new Customer
        {
            Id = SqliteHelper.GetGuid(reader, "Id"),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber")),
            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
            Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? null : reader.GetString(reader.GetOrdinal("Address")),
            CreatedAtUtc = SqliteHelper.GetDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = SqliteHelper.GetNullableDateTime(reader, "UpdatedAtUtc"),
            IsDeleted = SqliteHelper.GetBoolean(reader, "IsDeleted"),
            DeletedAtUtc = SqliteHelper.GetNullableDateTime(reader, "DeletedAtUtc")
        };
    }
}
