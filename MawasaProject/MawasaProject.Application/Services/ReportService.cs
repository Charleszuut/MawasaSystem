using System.Text;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class ReportService(IBillRepository billRepository, IPaymentRepository paymentRepository) : IReportService
{
    public async Task<string> GenerateRevenueReportCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var bills = await billRepository.ListAsync(cancellationToken);
        var start = filter.StartDateUtc ?? DateTime.MinValue;
        var end = filter.EndDateUtc ?? DateTime.MaxValue;

        var rows = bills
            .Where(b => b.CreatedAtUtc >= start && b.CreatedAtUtc <= end)
            .OrderBy(b => b.CreatedAtUtc)
            .Select(b => $"{b.BillNumber},{b.Amount:F2},{b.Balance:F2},{b.Status}");

        return BuildCsv("BillNumber,Amount,Balance,Status", rows);
    }

    public async Task<string> GenerateOverdueReportCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var bills = await billRepository.GetOverdueBillsAsync(DateTime.UtcNow, cancellationToken);
        var rows = bills.Select(b => $"{b.BillNumber},{b.DueDateUtc:O},{b.Balance:F2}");
        return BuildCsv("BillNumber,DueDateUtc,Balance", rows);
    }

    public async Task<string> GeneratePaymentHistoryCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var payments = await paymentRepository.ListAsync(cancellationToken);
        var start = filter.StartDateUtc ?? DateTime.MinValue;
        var end = filter.EndDateUtc ?? DateTime.MaxValue;

        var rows = payments
            .Where(p => p.PaymentDateUtc >= start && p.PaymentDateUtc <= end)
            .OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => $"{p.Id},{p.BillId},{p.Amount:F2},{p.PaymentDateUtc:O},{p.Status}");

        return BuildCsv("PaymentId,BillId,Amount,PaymentDateUtc,Status", rows);
    }

    private static string BuildCsv(string header, IEnumerable<string> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(header);

        foreach (var row in rows)
        {
            builder.AppendLine(row);
        }

        return builder.ToString();
    }
}
