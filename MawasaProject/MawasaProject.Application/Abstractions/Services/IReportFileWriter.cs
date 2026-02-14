namespace MawasaProject.Application.Abstractions.Services;

public interface IReportFileWriter
{
    Task<string> WriteCsvAsync(string reportType, string csvContent, CancellationToken cancellationToken = default);
}
