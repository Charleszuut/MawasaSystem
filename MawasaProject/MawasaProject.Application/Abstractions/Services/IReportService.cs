using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Abstractions.Services;

public interface IReportService
{
    Task<string> GenerateRevenueReportCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<string> GenerateOverdueReportCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default);
    Task<string> GeneratePaymentHistoryCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default);
}
