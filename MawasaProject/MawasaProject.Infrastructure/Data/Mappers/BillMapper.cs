using Microsoft.Data.Sqlite;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Data.Mappers;

public static class BillMapper
{
    public static Bill FromReader(SqliteDataReader reader)
    {
        return new Bill
        {
            Id = SqliteHelper.GetGuid(reader, "Id"),
            CustomerId = SqliteHelper.GetGuid(reader, "CustomerId"),
            BillNumber = reader.GetString(reader.GetOrdinal("BillNumber")),
            Amount = SqliteHelper.GetDecimal(reader, "Amount"),
            Balance = SqliteHelper.GetDecimal(reader, "Balance"),
            DueDateUtc = SqliteHelper.GetDateTime(reader, "DueDateUtc"),
            PaidAtUtc = SqliteHelper.GetNullableDateTime(reader, "PaidAtUtc"),
            Status = (BillStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            CreatedByUserId = SqliteHelper.GetGuid(reader, "CreatedByUserId"),
            CreatedAtUtc = SqliteHelper.GetDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = SqliteHelper.GetNullableDateTime(reader, "UpdatedAtUtc"),
            IsDeleted = SqliteHelper.GetBoolean(reader, "IsDeleted"),
            DeletedAtUtc = SqliteHelper.GetNullableDateTime(reader, "DeletedAtUtc")
        };
    }

    public static BillDto ToBillDto(SqliteDataReader reader)
    {
        return new BillDto
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            BillNumber = reader.GetString(reader.GetOrdinal("BillNumber")),
            CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
            Amount = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Amount"))),
            Balance = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Balance"))),
            DueDateUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("DueDateUtc"))),
            Status = (BillStatus)reader.GetInt32(reader.GetOrdinal("Status"))
        };
    }
}
