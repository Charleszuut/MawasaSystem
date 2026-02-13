using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Printing;

public sealed class PrintQueueService(
    ISqliteConnectionManager connectionManager,
    IAppLogger<PrintQueueService> logger)
{
    public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await GetQueuedJobsAsync(cancellationToken);

        foreach (var job in jobs)
        {
            try
            {
                // Offline spooler simulation: in production this calls platform printer adapters.
                await UpdateStatusAsync(job.Id, "Completed", cancellationToken);
                await AddLogAsync(job.Id, "Completed", null, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Print job processing failed for {0}", job.Id);
                await UpdateStatusAsync(job.Id, "Failed", cancellationToken);
                await AddLogAsync(job.Id, "Failed", exception.Message, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<PrintJob>> GetQueuedJobsAsync(CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, TemplateName, PrinterName, Payload, RetryCount, Status, CreatedAtUtc, UpdatedAtUtc FROM PrintJobs WHERE Status IN ('Queued', 'Retrying') ORDER BY CreatedAtUtc;";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<PrintJob>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(new PrintJob
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    TemplateName = reader.GetString(1),
                    PrinterName = reader.GetString(2),
                    Payload = reader.GetString(3),
                    RetryCount = reader.GetInt32(4),
                    Status = reader.GetString(5),
                    CreatedAtUtc = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    UpdatedAtUtc = reader.IsDBNull(7)
                        ? null
                        : DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task UpdateStatusAsync(Guid printJobId, string status, CancellationToken cancellationToken)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE PrintJobs SET Status = $Status, UpdatedAtUtc = $UpdatedAtUtc WHERE Id = $Id;";
            command.Parameters.AddWithValue("$Status", status);
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
}
