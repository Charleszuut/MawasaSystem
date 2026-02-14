using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Printing;

public sealed class PrintQueueService(
    ISqliteConnectionManager connectionManager,
    IAppLogger<PrintQueueService> logger)
{
    private readonly SemaphoreSlim _processingGate = new(1, 1);

    public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        await _processingGate.WaitAsync(cancellationToken);
        try
        {
            var jobs = await GetQueuedJobsAsync(cancellationToken);

            foreach (var job in jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await UpdateStatusAsync(job.Id, "Processing", job.RetryCount, null, cancellationToken);
                    await SimulatePlatformPrintAsync(job, cancellationToken);
                    await UpdateStatusAsync(job.Id, "Completed", job.RetryCount, null, cancellationToken);
                    await AddLogAsync(job.Id, "Completed", null, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.Error(exception, "Print job processing failed for {0}", job.Id);

                    var nextRetryCount = job.RetryCount + 1;
                    var canRetry = nextRetryCount <= Math.Max(0, job.MaxRetries);
                    var status = canRetry ? "Retrying" : "Failed";

                    await UpdateStatusAsync(job.Id, status, nextRetryCount, exception.Message, cancellationToken);
                    await AddLogAsync(job.Id, status, exception.Message, cancellationToken);
                }
            }
        }
        finally
        {
            _processingGate.Release();
        }
    }

    public async Task RetryAsync(Guid printJobId, CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(printJobId, "Queued", retryCount: 0, lastError: null, cancellationToken);
        await AddLogAsync(printJobId, "Queued", "Manual retry requested.", cancellationToken);
        await ProcessQueueAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrintJob>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TemplateName, PrinterName, ProfileName, PaperSize, Payload, RetryCount, MaxRetries, LastError, LastTriedAtUtc, Status, CreatedAtUtc, UpdatedAtUtc
            FROM PrintJobs
            ORDER BY CreatedAtUtc DESC;
            """;

        return await QueryJobsAsync(sql, cancellationToken);
    }

    public async Task<IReadOnlyList<PrintLog>> GetLogsAsync(Guid? printJobId = null, CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = printJobId.HasValue
                ? "SELECT Id, PrintJobId, Status, Error, LoggedAtUtc, CreatedAtUtc, UpdatedAtUtc FROM PrintLogs WHERE PrintJobId = $PrintJobId ORDER BY LoggedAtUtc DESC;"
                : "SELECT Id, PrintJobId, Status, Error, LoggedAtUtc, CreatedAtUtc, UpdatedAtUtc FROM PrintLogs ORDER BY LoggedAtUtc DESC LIMIT 250;";
            if (printJobId.HasValue)
            {
                command.Parameters.AddWithValue("$PrintJobId", printJobId.Value.ToString());
            }

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<PrintLog>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(new PrintLog
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    PrintJobId = Guid.Parse(reader.GetString(1)),
                    Status = reader.GetString(2),
                    Error = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LoggedAtUtc = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    CreatedAtUtc = DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    UpdatedAtUtc = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task<IReadOnlyList<PrintJob>> GetQueuedJobsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, TemplateName, PrinterName, ProfileName, PaperSize, Payload, RetryCount, MaxRetries, LastError, LastTriedAtUtc, Status, CreatedAtUtc, UpdatedAtUtc
            FROM PrintJobs
            WHERE Status IN ('Queued', 'Retrying')
            ORDER BY CreatedAtUtc;
            """;

        return await QueryJobsAsync(sql, cancellationToken);
    }

    private async Task<IReadOnlyList<PrintJob>> QueryJobsAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<PrintJob>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(new PrintJob
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    TemplateName = reader.GetString(1),
                    PrinterName = reader.GetString(2),
                    ProfileName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PaperSize = reader.IsDBNull(4) ? "A4" : reader.GetString(4),
                    Payload = reader.GetString(5),
                    RetryCount = reader.GetInt32(6),
                    MaxRetries = reader.IsDBNull(7) ? 3 : reader.GetInt32(7),
                    LastError = reader.IsDBNull(8) ? null : reader.GetString(8),
                    LastTriedAtUtc = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    Status = reader.GetString(10),
                    CreatedAtUtc = DateTime.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    UpdatedAtUtc = reader.IsDBNull(12) ? null : DateTime.Parse(reader.GetString(12), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task UpdateStatusAsync(Guid printJobId, string status, int retryCount, string? lastError, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PrintJobs
                SET Status = $Status,
                    RetryCount = $RetryCount,
                    LastError = $LastError,
                    LastTriedAtUtc = $LastTriedAtUtc,
                    UpdatedAtUtc = $UpdatedAtUtc
                WHERE Id = $Id;
                """;
            command.Parameters.AddWithValue("$Status", status);
            command.Parameters.AddWithValue("$RetryCount", retryCount);
            command.Parameters.AddWithValue("$LastError", (object?)lastError ?? DBNull.Value);
            command.Parameters.AddWithValue("$LastTriedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$Id", printJobId.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task AddLogAsync(Guid printJobId, string status, string? error, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO PrintLogs (Id, PrintJobId, Status, Error, LoggedAtUtc, CreatedAtUtc, UpdatedAtUtc) VALUES ($Id, $PrintJobId, $Status, $Error, $LoggedAtUtc, $CreatedAtUtc, $UpdatedAtUtc);";
            command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$PrintJobId", printJobId.ToString());
            command.Parameters.AddWithValue("$Status", status);
            command.Parameters.AddWithValue("$Error", (object?)error ?? DBNull.Value);
            command.Parameters.AddWithValue("$LoggedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$CreatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private static async Task SimulatePlatformPrintAsync(PrintJob job, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken);

        if (string.IsNullOrWhiteSpace(job.PrinterName))
        {
            throw new InvalidOperationException("Printer name is not configured.");
        }

        if (job.Payload.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated print engine failure.");
        }
    }
}
