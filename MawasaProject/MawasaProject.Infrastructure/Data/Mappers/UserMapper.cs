using Microsoft.Data.Sqlite;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Data.Mappers;

public static class UserMapper
{
    public static User FromReader(SqliteDataReader reader)
    {
        return new User
        {
            Id = SqliteHelper.GetGuid(reader, "Id"),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            PasswordSalt = reader.GetString(reader.GetOrdinal("PasswordSalt")),
            IsActive = SqliteHelper.GetBoolean(reader, "IsActive"),
            LastLoginAtUtc = SqliteHelper.GetNullableDateTime(reader, "LastLoginAtUtc"),
            CreatedAtUtc = SqliteHelper.GetDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = SqliteHelper.GetNullableDateTime(reader, "UpdatedAtUtc"),
            IsDeleted = SqliteHelper.GetBoolean(reader, "IsDeleted"),
            DeletedAtUtc = SqliteHelper.GetNullableDateTime(reader, "DeletedAtUtc")
        };
    }
}
