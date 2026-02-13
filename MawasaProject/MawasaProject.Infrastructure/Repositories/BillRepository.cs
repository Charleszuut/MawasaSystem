using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class BillRepository(ISqliteConnectionManager connectionManager)
    : GenericRepository<Bill>(connectionManager), IBillRepository
{
    protected override string TableName => "Bills";
    protected override string GetByIdSql => "SELECT * FROM Bills WHERE Id = $Id AND IsDeleted = 0 LIMIT 1;";
    protected override string ListSql => "SELECT * FROM Bills WHERE IsDeleted = 0 ORDER BY DueDateUtc;";
    protected override string DeleteSql => "UPDATE Bills SET IsDeleted = 1, DeletedAtUtc = $DeletedAtUtc WHERE Id = $Id;";

    protected override string InsertSql =>
        "INSERT INTO Bills (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc) VALUES ($Id, $CustomerId, $BillNumber, $Amount, $Balance, $DueDateUtc, $PaidAtUtc, $Status, $CreatedByUserId, $CreatedAtUtc, $UpdatedAtUtc, $IsDeleted, $DeletedAtUtc);";

    protected override string UpdateSql =>
        "UPDATE Bills SET CustomerId = $CustomerId, BillNumber = $BillNumber, Amount = $Amount, Balance = $Balance, DueDateUtc = $DueDateUtc, PaidAtUtc = $PaidAtUtc, Status = $Status, CreatedByUserId = $CreatedByUserId, UpdatedAtUtc = $UpdatedAtUtc, IsDeleted = $IsDeleted, DeletedAtUtc = $DeletedAtUtc WHERE Id = $Id;";

    protected override Bill Map(SqliteDataReader reader)
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

    protected override void BindInsert(SqliteCommand command, Bill entity)
    {
        command.Parameters.AddWithValue("$Id", entity.Id.ToString());
        command.Parameters.AddWithValue("$CustomerId", entity.CustomerId.ToString());
        command.Parameters.AddWithValue("$BillNumber", entity.BillNumber);
        command.Parameters.AddWithValue("$Amount", entity.Amount);
        command.Parameters.AddWithValue("$Balance", entity.Balance);
        command.Parameters.AddWithValue("$DueDateUtc", entity.DueDateUtc.ToString("O"));
        command.Parameters.AddWithValue("$PaidAtUtc", (object?)entity.PaidAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$Status", (int)entity.Status);
        command.Parameters.AddWithValue("$CreatedByUserId", entity.CreatedByUserId.ToString());
        command.Parameters.AddWithValue("$CreatedAtUtc", entity.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$UpdatedAtUtc", (object?)entity.UpdatedAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$IsDeleted", entity.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("$DeletedAtUtc", (object?)entity.DeletedAtUtc?.ToString("O") ?? DBNull.Value);
    }

    protected override void BindUpdate(SqliteCommand command, Bill entity)
    {
        BindInsert(command, entity);
    }

    public async Task<IReadOnlyList<Bill>> GetOverdueBillsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM Bills
            WHERE IsDeleted = 0
              AND Status <> 2
              AND DueDateUtc < $AsOfUtc
            ORDER BY DueDateUtc;
            """;

        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$AsOfUtc", asOfUtc.ToString("O"));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<Bill>();
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

    public async Task<IReadOnlyList<BillDto>> GetBillsByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT b.Id, b.BillNumber, c.Name AS CustomerName, b.Amount, b.Balance, b.DueDateUtc, b.Status
            FROM Bills b
            INNER JOIN Customers c ON c.Id = b.CustomerId
            WHERE b.CustomerId = $CustomerId AND b.IsDeleted = 0
            ORDER BY b.DueDateUtc DESC;
            """;

        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$CustomerId", customerId.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<BillDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(new BillDto
                {
                    Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
                    BillNumber = reader.GetString(reader.GetOrdinal("BillNumber")),
                    CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                    Amount = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Amount"))),
                    Balance = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Balance"))),
                    DueDateUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("DueDateUtc"))),
                    Status = (BillStatus)reader.GetInt32(reader.GetOrdinal("Status"))
                });
            }

            return output;
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            var totalRevenue = await ExecuteScalarDecimalAsync(connection, "SELECT COALESCE(SUM(Amount), 0) FROM Payments WHERE Status = 2 AND PaymentDateUtc BETWEEN $FromUtc AND $ToUtc;", fromUtc, toUtc, cancellationToken);
            var outstanding = await ExecuteScalarDecimalAsync(connection, "SELECT COALESCE(SUM(Balance), 0) FROM Bills WHERE IsDeleted = 0 AND Status <> 2;", null, null, cancellationToken);
            var pending = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Bills WHERE IsDeleted = 0 AND Status = 1;", null, null, cancellationToken);
            var overdue = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Bills WHERE IsDeleted = 0 AND Status = 3;", null, null, cancellationToken);
            var customers = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Customers WHERE IsDeleted = 0;", null, null, cancellationToken);

            var series = new List<MonthlyRevenuePointDto>();
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = """
                SELECT substr(PaymentDateUtc, 1, 4) AS YearPart,
                       substr(PaymentDateUtc, 6, 2) AS MonthPart,
                       COALESCE(SUM(Amount), 0) AS Revenue
                FROM Payments
                WHERE Status = 2 AND PaymentDateUtc BETWEEN $FromUtc AND $ToUtc
                GROUP BY YearPart, MonthPart
                ORDER BY YearPart, MonthPart;
                """;
            command.Parameters.AddWithValue("$FromUtc", fromUtc.ToString("O"));
            command.Parameters.AddWithValue("$ToUtc", toUtc.ToString("O"));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                series.Add(new MonthlyRevenuePointDto
                {
                    Year = int.Parse(reader.GetString(reader.GetOrdinal("YearPart"))),
                    Month = int.Parse(reader.GetString(reader.GetOrdinal("MonthPart"))),
                    Revenue = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Revenue")))
                });
            }

            return new DashboardSummaryDto
            {
                TotalRevenue = totalRevenue,
                OutstandingBalance = outstanding,
                PendingBills = pending,
                OverdueBills = overdue,
                TotalCustomers = customers,
                RevenueSeries = series
            };
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = DeleteSql;
            command.Parameters.AddWithValue("$Id", id.ToString());
            command.Parameters.AddWithValue("$DeletedAtUtc", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task<decimal> ExecuteScalarDecimalAsync(SqliteConnection connection, string sql, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = ConnectionManager.CurrentTransaction;
        command.CommandText = sql;

        if (fromUtc.HasValue)
        {
            command.Parameters.AddWithValue("$FromUtc", fromUtc.Value.ToString("O"));
        }

        if (toUtc.HasValue)
        {
            command.Parameters.AddWithValue("$ToUtc", toUtc.Value.ToString("O"));
        }

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToDecimal(value);
    }

    private async Task<int> ExecuteScalarIntAsync(SqliteConnection connection, string sql, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = ConnectionManager.CurrentTransaction;
        command.CommandText = sql;

        if (fromUtc.HasValue)
        {
            command.Parameters.AddWithValue("$FromUtc", fromUtc.Value.ToString("O"));
        }

        if (toUtc.HasValue)
        {
            command.Parameters.AddWithValue("$ToUtc", toUtc.Value.ToString("O"));
        }

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }
}
