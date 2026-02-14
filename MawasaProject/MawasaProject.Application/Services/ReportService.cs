using System.Globalization;
using System.Text;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Application.Services;

public sealed class ReportService(
    IBillRepository billRepository,
    IPaymentRepository paymentRepository,
    IAuditInterceptor auditInterceptor) : IReportService
{
    public async Task<string> GenerateRevenueReportCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = ResolveDateRange(filter);
        var bills = await billRepository.ListAsync(cancellationToken);

        var query = bills
            .Where(b => b.CreatedAtUtc >= startUtc && b.CreatedAtUtc <= endUtc);

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(b => b.CustomerId == filter.CustomerId.Value);
        }

        if (filter.IncludeOverdueOnly)
        {
            query = query.Where(b => b.Status == BillStatus.Overdue);
        }

        var rows = query
            .OrderBy(b => b.CreatedAtUtc)
            .Select(b => BuildRow(
                b.BillNumber,
                b.Amount.ToString("F2", CultureInfo.InvariantCulture),
                b.Balance.ToString("F2", CultureInfo.InvariantCulture),
                b.Status.ToString(),
                b.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)))
            .ToList();

        var csv = BuildCsv("BillNumber,Amount,Balance,Status,CreatedAtUtc", rows);
        await auditInterceptor.TrackAsync(
            AuditActionType.Export,
            "Report",
            entityId: null,
            oldValue: null,
            newValue: new
            {
                Type = "Revenue",
                Filter = filter,
                RowCount = rows.Count
            },
            context: "Revenue report exported",
            username: null,
            cancellationToken);

        return csv;
    }

    public async Task<string> GenerateOverdueReportCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = ResolveDateRange(filter);
        var bills = await billRepository.GetOverdueBillsAsync(DateTime.UtcNow, cancellationToken);

        var query = bills
            .Where(b => b.DueDateUtc >= startUtc && b.DueDateUtc <= endUtc);

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(b => b.CustomerId == filter.CustomerId.Value);
        }

        var rows = query
            .OrderBy(b => b.DueDateUtc)
            .Select(b => BuildRow(
                b.BillNumber,
                b.CustomerId.ToString(),
                b.DueDateUtc.ToString("O", CultureInfo.InvariantCulture),
                b.Balance.ToString("F2", CultureInfo.InvariantCulture)))
            .ToList();

        var csv = BuildCsv("BillNumber,CustomerId,DueDateUtc,Balance", rows);
        await auditInterceptor.TrackAsync(
            AuditActionType.Export,
            "Report",
            entityId: null,
            oldValue: null,
            newValue: new
            {
                Type = "Overdue",
                Filter = filter,
                RowCount = rows.Count
            },
            context: "Overdue report exported",
            username: null,
            cancellationToken);

        return csv;
    }

    public async Task<string> GeneratePaymentHistoryCsvAsync(ReportFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = ResolveDateRange(filter);
        var payments = await paymentRepository.ListAsync(cancellationToken);
        IReadOnlyList<Bill> bills = [];
        var billLookup = new Dictionary<Guid, Bill>();

        if (filter.CustomerId.HasValue || filter.IncludeOverdueOnly)
        {
            bills = await billRepository.ListAsync(cancellationToken);
            billLookup = bills.ToDictionary(b => b.Id);
        }

        var query = payments
            .Where(p => p.PaymentDateUtc >= startUtc && p.PaymentDateUtc <= endUtc);

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(p =>
                billLookup.TryGetValue(p.BillId, out var bill) &&
                bill.CustomerId == filter.CustomerId.Value);
        }

        if (filter.IncludeOverdueOnly)
        {
            query = query.Where(p =>
                billLookup.TryGetValue(p.BillId, out var bill) &&
                bill.Status == BillStatus.Overdue);
        }

        var rows = query
            .OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => BuildRow(
                p.Id.ToString(),
                p.BillId.ToString(),
                p.Amount.ToString("F2", CultureInfo.InvariantCulture),
                p.PaymentDateUtc.ToString("O", CultureInfo.InvariantCulture),
                p.Status.ToString(),
                p.ReferenceNumber))
            .ToList();

        var csv = BuildCsv("PaymentId,BillId,Amount,PaymentDateUtc,Status,ReferenceNumber", rows);
        await auditInterceptor.TrackAsync(
            AuditActionType.Export,
            "Report",
            entityId: null,
            oldValue: null,
            newValue: new
            {
                Type = "PaymentHistory",
                Filter = filter,
                RowCount = rows.Count
            },
            context: "Payment history report exported",
            username: null,
            cancellationToken);

        return csv;
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

    private static (DateTime StartUtc, DateTime EndUtc) ResolveDateRange(ReportFilterDto filter)
    {
        var start = filter.StartDateUtc ?? DateTime.MinValue;
        var end = filter.EndDateUtc ?? DateTime.MaxValue;
        if (start > end)
        {
            throw new InvalidOperationException("Report start date cannot be later than end date.");
        }

        return (start, end);
    }

    private static string BuildRow(params string?[] values)
    {
        return string.Join(",", values.Select(EscapeCsv));
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        if (escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
        {
            return "\"" + escaped + "\"";
        }

        return escaped;
    }
}
