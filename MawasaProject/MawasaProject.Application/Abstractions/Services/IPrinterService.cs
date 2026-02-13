namespace MawasaProject.Application.Abstractions.Services;

public sealed class PrintRequest
{
    public required string TemplateName { get; init; }
    public required string Content { get; init; }
    public string? PrinterName { get; init; }
    public int RetryCount { get; init; }
}

public interface IPrinterService
{
    Task<IReadOnlyList<string>> GetInstalledPrintersAsync(CancellationToken cancellationToken = default);
    Task<Guid> EnqueueAsync(PrintRequest request, CancellationToken cancellationToken = default);
}
