using MawasaProject.Domain.Entities;

namespace MawasaProject.Application.Abstractions.Services;

public sealed class PrintRequest
{
    public required string TemplateName { get; init; }
    public required string Content { get; init; }
    public string? PrinterName { get; init; }
    public string? ProfileName { get; init; }
    public string PaperSize { get; init; } = "A4";
    public int MaxRetries { get; init; } = 2;
}

public interface IPrinterService
{
    Task<IReadOnlyList<string>> GetInstalledPrintersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrinterProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task SaveProfileAsync(PrinterProfile profile, CancellationToken cancellationToken = default);
    Task SetDefaultPrinterAsync(string deviceName, CancellationToken cancellationToken = default);
    Task<Guid> EnqueueAsync(PrintRequest request, CancellationToken cancellationToken = default);
    Task ProcessQueueAsync(CancellationToken cancellationToken = default);
    Task RetryAsync(Guid printJobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrintJob>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrintLog>> GetLogsAsync(Guid? printJobId = null, CancellationToken cancellationToken = default);
}
