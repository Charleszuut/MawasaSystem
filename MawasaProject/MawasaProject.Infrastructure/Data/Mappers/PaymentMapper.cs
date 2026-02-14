using Microsoft.Data.Sqlite;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Data.Mappers;

public static class PaymentMapper
{
    public static Payment FromReader(SqliteDataReader reader)
    {
        return new Payment
        {
            Id = SqliteHelper.GetGuid(reader, "Id"),
            BillId = SqliteHelper.GetGuid(reader, "BillId"),
            Amount = SqliteHelper.GetDecimal(reader, "Amount"),
            PaymentDateUtc = SqliteHelper.GetDateTime(reader, "PaymentDateUtc"),
            Status = (PaymentStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            ReferenceNumber = reader.IsDBNull(reader.GetOrdinal("ReferenceNumber")) ? null : reader.GetString(reader.GetOrdinal("ReferenceNumber")),
            CreatedByUserId = SqliteHelper.GetGuid(reader, "CreatedByUserId"),
            CreatedAtUtc = SqliteHelper.GetDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = SqliteHelper.GetNullableDateTime(reader, "UpdatedAtUtc")
        };
    }
}
