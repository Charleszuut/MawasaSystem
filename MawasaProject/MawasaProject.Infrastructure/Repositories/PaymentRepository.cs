using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class PaymentRepository(ISqliteConnectionManager connectionManager)
    : GenericRepository<Payment>(connectionManager), IPaymentRepository
{
    protected override string TableName => "Payments";

    protected override string InsertSql =>
        "INSERT INTO Payments (Id, BillId, Amount, PaymentDateUtc, Status, ReferenceNumber, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc) VALUES ($Id, $BillId, $Amount, $PaymentDateUtc, $Status, $ReferenceNumber, $CreatedByUserId, $CreatedAtUtc, $UpdatedAtUtc);";

    protected override string UpdateSql =>
        "UPDATE Payments SET BillId = $BillId, Amount = $Amount, PaymentDateUtc = $PaymentDateUtc, Status = $Status, ReferenceNumber = $ReferenceNumber, CreatedByUserId = $CreatedByUserId, UpdatedAtUtc = $UpdatedAtUtc WHERE Id = $Id;";

    protected override Payment Map(SqliteDataReader reader)
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

    protected override void BindInsert(SqliteCommand command, Payment entity)
    {
        command.Parameters.AddWithValue("$Id", entity.Id.ToString());
        command.Parameters.AddWithValue("$BillId", entity.BillId.ToString());
        command.Parameters.AddWithValue("$Amount", entity.Amount);
        command.Parameters.AddWithValue("$PaymentDateUtc", entity.PaymentDateUtc.ToString("O"));
        command.Parameters.AddWithValue("$Status", (int)entity.Status);
        command.Parameters.AddWithValue("$ReferenceNumber", (object?)entity.ReferenceNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$CreatedByUserId", entity.CreatedByUserId.ToString());
        command.Parameters.AddWithValue("$CreatedAtUtc", entity.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$UpdatedAtUtc", (object?)entity.UpdatedAtUtc?.ToString("O") ?? DBNull.Value);
    }

    protected override void BindUpdate(SqliteCommand command, Payment entity)
    {
        BindInsert(command, entity);
    }

    public async Task<IReadOnlyList<Payment>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Payments WHERE BillId = $BillId ORDER BY PaymentDateUtc DESC;";
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$BillId", billId.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<Payment>();
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

    public async Task<decimal> GetTotalPaidForBillAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COALESCE(SUM(Amount), 0) FROM Payments WHERE BillId = $BillId AND Status = 2;";
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$BillId", billId.ToString());

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToDecimal(value);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }
}
