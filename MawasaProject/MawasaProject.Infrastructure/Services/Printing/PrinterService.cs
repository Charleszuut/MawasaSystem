using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Printing;

public sealed class PrinterService(
    ISqliteConnectionManager connectionManager,
    PrintQueueService printQueueService) : IPrinterService
{
    public async Task<IReadOnlyList<string>> GetInstalledPrintersAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DeviceName FROM PrinterProfiles ORDER BY IsDefault DESC, Name ASC;";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(reader.GetString(0));
            }

            if (output.Count == 0)
            {
                output.Add("Default-Printer");
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<Guid> EnqueueAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var printerName = request.PrinterName ?? "Default-Printer";

        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO PrintJobs (Id, TemplateName, PrinterName, Payload, RetryCount, Status, CreatedAtUtc, UpdatedAtUtc) VALUES ($Id, $TemplateName, $PrinterName, $Payload, $RetryCount, $Status, $CreatedAtUtc, $UpdatedAtUtc);";
            command.Parameters.AddWithValue("$Id", jobId.ToString());
            command.Parameters.AddWithValue("$TemplateName", request.TemplateName);
            command.Parameters.AddWithValue("$PrinterName", printerName);
            command.Parameters.AddWithValue("$Payload", request.Content);
            command.Parameters.AddWithValue("$RetryCount", request.RetryCount);
            command.Parameters.AddWithValue("$Status", "Queued");
            command.Parameters.AddWithValue("$CreatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }

        await printQueueService.ProcessQueueAsync(cancellationToken);
        return jobId;
    }
}
