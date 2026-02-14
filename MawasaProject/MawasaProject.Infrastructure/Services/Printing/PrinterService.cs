using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Printing;

public sealed class PrinterService(
    ISqliteConnectionManager connectionManager,
    PrintQueueService printQueueService) : IPrinterService
{
    public async Task<IReadOnlyList<string>> GetInstalledPrintersAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await GetProfilesAsync(cancellationToken);
        var output = profiles
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .Select(p => p.DeviceName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (output.Count == 0)
        {
            output.Add("Default-Printer");
        }

        return output;
    }

    public async Task<IReadOnlyList<PrinterProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, Name, DeviceName, PaperSize, IsDefault, IsActive, CreatedAtUtc, UpdatedAtUtc
                FROM PrinterProfiles
                ORDER BY IsDefault DESC, Name ASC;
                """;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<PrinterProfile>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(new PrinterProfile
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    DeviceName = reader.GetString(2),
                    PaperSize = reader.GetString(3),
                    IsDefault = reader.GetInt32(4) == 1,
                    IsActive = reader.GetInt32(5) == 1,
                    CreatedAtUtc = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    UpdatedAtUtc = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task SaveProfileAsync(PrinterProfile profile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Name) || string.IsNullOrWhiteSpace(profile.DeviceName))
        {
            throw new InvalidOperationException("Profile name and device name are required.");
        }

        profile.PaperSize = string.IsNullOrWhiteSpace(profile.PaperSize) ? "A4" : profile.PaperSize.Trim();
        if (profile.Id == Guid.Empty)
        {
            profile.Id = Guid.NewGuid();
        }

        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            if (profile.IsDefault)
            {
                using var reset = connection.CreateCommand();
                reset.CommandText = "UPDATE PrinterProfiles SET IsDefault = 0, UpdatedAtUtc = $UpdatedAtUtc;";
                reset.Parameters.AddWithValue("$UpdatedAtUtc", DateTime.UtcNow.ToString("O"));
                await reset.ExecuteNonQueryAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PrinterProfiles (Id, Name, DeviceName, PaperSize, IsDefault, IsActive, CreatedAtUtc, UpdatedAtUtc)
                VALUES ($Id, $Name, $DeviceName, $PaperSize, $IsDefault, $IsActive, $CreatedAtUtc, $UpdatedAtUtc)
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    DeviceName = excluded.DeviceName,
                    PaperSize = excluded.PaperSize,
                    IsDefault = excluded.IsDefault,
                    IsActive = excluded.IsActive,
                    UpdatedAtUtc = excluded.UpdatedAtUtc;
                """;
            command.Parameters.AddWithValue("$Id", profile.Id.ToString());
            command.Parameters.AddWithValue("$Name", profile.Name.Trim());
            command.Parameters.AddWithValue("$DeviceName", profile.DeviceName.Trim());
            command.Parameters.AddWithValue("$PaperSize", profile.PaperSize);
            command.Parameters.AddWithValue("$IsDefault", profile.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("$IsActive", profile.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$CreatedAtUtc", profile.CreatedAtUtc == default ? DateTime.UtcNow.ToString("O") : profile.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task SetDefaultPrinterAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new InvalidOperationException("Device name is required.");
        }

        var profiles = await GetProfilesAsync(cancellationToken);
        var existing = profiles.FirstOrDefault(p => string.Equals(p.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new PrinterProfile
            {
                Id = Guid.NewGuid(),
                Name = deviceName,
                DeviceName = deviceName,
                PaperSize = "A4",
                IsDefault = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
        else
        {
            existing.IsDefault = true;
            existing.IsActive = true;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await SaveProfileAsync(existing, cancellationToken);
    }

    public async Task<Guid> EnqueueAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            throw new InvalidOperationException("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Print content is required.");
        }

        var profile = await ResolveProfileAsync(request, cancellationToken);
        var printerName = request.PrinterName
            ?? profile?.DeviceName
            ?? "Default-Printer";
        var paperSize = string.IsNullOrWhiteSpace(request.PaperSize)
            ? profile?.PaperSize ?? "A4"
            : request.PaperSize.Trim();
        var profileName = request.ProfileName ?? profile?.Name;

        var jobId = Guid.NewGuid();
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PrintJobs
                    (Id, TemplateName, PrinterName, ProfileName, PaperSize, Payload, RetryCount, MaxRetries, LastError, LastTriedAtUtc, Status, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    ($Id, $TemplateName, $PrinterName, $ProfileName, $PaperSize, $Payload, $RetryCount, $MaxRetries, $LastError, $LastTriedAtUtc, $Status, $CreatedAtUtc, $UpdatedAtUtc);
                """;
            command.Parameters.AddWithValue("$Id", jobId.ToString());
            command.Parameters.AddWithValue("$TemplateName", request.TemplateName);
            command.Parameters.AddWithValue("$PrinterName", printerName);
            command.Parameters.AddWithValue("$ProfileName", (object?)profileName ?? DBNull.Value);
            command.Parameters.AddWithValue("$PaperSize", paperSize);
            command.Parameters.AddWithValue("$Payload", request.Content);
            command.Parameters.AddWithValue("$RetryCount", 0);
            command.Parameters.AddWithValue("$MaxRetries", Math.Max(0, request.MaxRetries));
            command.Parameters.AddWithValue("$LastError", DBNull.Value);
            command.Parameters.AddWithValue("$LastTriedAtUtc", DBNull.Value);
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

    public Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        return printQueueService.ProcessQueueAsync(cancellationToken);
    }

    public Task RetryAsync(Guid printJobId, CancellationToken cancellationToken = default)
    {
        return printQueueService.RetryAsync(printJobId, cancellationToken);
    }

    public Task<IReadOnlyList<PrintJob>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        return printQueueService.GetQueueAsync(cancellationToken);
    }

    public Task<IReadOnlyList<PrintLog>> GetLogsAsync(Guid? printJobId = null, CancellationToken cancellationToken = default)
    {
        return printQueueService.GetLogsAsync(printJobId, cancellationToken);
    }

    private async Task<PrinterProfile?> ResolveProfileAsync(PrintRequest request, CancellationToken cancellationToken)
    {
        var profiles = await GetProfilesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ProfileName))
        {
            return profiles.FirstOrDefault(p => string.Equals(p.Name, request.ProfileName, StringComparison.OrdinalIgnoreCase));
        }

        return profiles.FirstOrDefault(p => p.IsDefault && p.IsActive);
    }
}
