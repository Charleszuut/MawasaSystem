using MawasaProject.Application.Abstractions.Services;

namespace MawasaProject.Infrastructure.Services.Reports;

public sealed class OfflineReportFileWriter : IReportFileWriter
{
    public async Task<string> WriteCsvAsync(string reportType, string csvContent, CancellationToken cancellationToken = default)
    {
        var safeType = string.IsNullOrWhiteSpace(reportType)
            ? "report"
            : string.Concat(reportType
                .Trim()
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(safeType))
        {
            safeType = "report";
        }

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MawasaProject",
            "exports");
        Directory.CreateDirectory(root);

        var fileName = $"{safeType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        var path = Path.Combine(root, fileName);

        await File.WriteAllTextAsync(path, csvContent, cancellationToken);
        return path;
    }
}
