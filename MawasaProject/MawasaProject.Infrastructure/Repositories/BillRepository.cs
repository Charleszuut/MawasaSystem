using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.Mappers;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class BillRepository(
    ISqliteConnectionManager connectionManager,
    SqliteDatabaseOptions options,
    IAppLogger<BillRepository> logger)
    : GenericRepository<Bill>(connectionManager, options), IBillRepository
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
        return BillMapper.FromReader(reader);
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
            using var command = CreateCommand(connection, sql);
            command.Parameters.AddWithValue("$AsOfUtc", asOfUtc.ToString("O"));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<Bill>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(Map(reader));
            }

            return output;
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to list overdue bills as of {0}", asOfUtc);
            throw CreateRepositoryException("GetOverdueBills", exception);
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
            using var command = CreateCommand(connection, sql);
            command.Parameters.AddWithValue("$CustomerId", customerId.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<BillDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(BillMapper.ToBillDto(reader));
            }

            return output;
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to list bills for customer {0}", customerId);
            throw CreateRepositoryException("GetBillsByCustomer", exception);
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
            var totalBills = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Bills WHERE IsDeleted = 0;", null, null, cancellationToken);
            var pending = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Bills WHERE IsDeleted = 0 AND Status = 1;", null, null, cancellationToken);
            var overdue = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Bills WHERE IsDeleted = 0 AND Status = 3;", null, null, cancellationToken);
            var customers = await ExecuteScalarIntAsync(connection, "SELECT COUNT(1) FROM Customers WHERE IsDeleted = 0;", null, null, cancellationToken);

            var series = new List<MonthlyRevenuePointDto>();
            using var command = CreateCommand(connection, """
                SELECT substr(PaymentDateUtc, 1, 4) AS YearPart,
                       substr(PaymentDateUtc, 6, 2) AS MonthPart,
                       COALESCE(SUM(Amount), 0) AS Revenue
                FROM Payments
                WHERE Status = 2 AND PaymentDateUtc BETWEEN $FromUtc AND $ToUtc
                GROUP BY YearPart, MonthPart
                ORDER BY YearPart, MonthPart;
                """);
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
                TotalBills = totalBills,
                PendingBills = pending,
                OverdueBills = overdue,
                TotalCustomers = customers,
                RevenueSeries = series
            };
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to calculate dashboard summary from {0} to {1}", fromUtc, toUtc);
            throw CreateRepositoryException("GetDashboardSummary", exception);
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
            using var command = CreateCommand(connection, DeleteSql);
            command.Parameters.AddWithValue("$Id", id.ToString());
            command.Parameters.AddWithValue("$DeletedAtUtc", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to soft delete bill {0}", id);
            throw CreateRepositoryException("Delete", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task<decimal> ExecuteScalarDecimalAsync(SqliteConnection connection, string sql, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, sql);

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
        using var command = CreateCommand(connection, sql);

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
