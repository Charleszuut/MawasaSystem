using Microsoft.Data.Sqlite;

namespace MawasaProject.Infrastructure.Data.SQLite;

public static class SqliteHelper
{
    public static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    public static Guid GetGuid(SqliteDataReader reader, string column)
    {
        var value = reader.GetString(reader.GetOrdinal(column));
        return Guid.Parse(value);
    }

    public static DateTime GetDateTime(SqliteDataReader reader, string column)
    {
        var value = reader.GetString(reader.GetOrdinal(column));
        return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static DateTime? GetNullableDateTime(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal);
        return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static decimal GetDecimal(SqliteDataReader reader, string column)
    {
        return Convert.ToDecimal(reader.GetValue(reader.GetOrdinal(column)));
    }

    public static bool GetBoolean(SqliteDataReader reader, string column)
    {
        return reader.GetInt32(reader.GetOrdinal(column)) == 1;
    }
}
