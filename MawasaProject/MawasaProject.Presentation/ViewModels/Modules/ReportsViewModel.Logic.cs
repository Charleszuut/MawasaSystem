using System.Globalization;
using System.Text;
using System.Text.Json;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Domain.Enums;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed partial class ReportsViewModel
{
    public void SetModeFromRoute(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        var parsedMode = mode.Trim().ToLowerInvariant() switch
        {
            "issue" or "issues" => ReportMode.Issues,
            "print" or "printable" or "printables" => ReportMode.Printables,
            _ => ReportMode.CustomerPayments
        };

        SetMode(parsedMode);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await ApplyFiltersInternalAsync(cancellationToken);
    }

    private async Task ApplyFiltersInternalAsync(CancellationToken cancellationToken)
    {
        if (StartDateUtc.Date > EndDateUtc.Date)
        {
            await _dialogService.AlertAsync("Validation", "Start date must be earlier than or equal to end date.");
            return;
        }

        var filter = await BuildFilterAsync(cancellationToken);
        if (filter is null)
        {
            return;
        }

        switch (_selectedMode)
        {
            case ReportMode.Issues:
                await LoadIssueReportAsync(filter, cancellationToken);
                break;
            case ReportMode.Printables:
                await LoadPrintableReportAsync(filter, cancellationToken);
                break;
            default:
                await LoadCustomerPaymentReportAsync(filter, cancellationToken);
                break;
        }

        ApplyCurrentModeSearchSort();
        StatusMessage = "Report view refreshed.";
    }

    private async Task LoadCustomerPaymentReportAsync(ReportFilterDto filter, CancellationToken cancellationToken)
    {
        var bills = await _billingService.GetBillsAsync(cancellationToken);
        var filteredBills = bills
            .Where(b => IsWithinRange(b.CreatedAtUtc, filter.StartDateUtc, filter.EndDateUtc))
            .Where(b => !filter.CustomerId.HasValue || b.CustomerId == filter.CustomerId.Value)
            .ToList();

        var paymentsByBill = await LoadPaymentsByBillAsync(filteredBills, cancellationToken);

        CollectedAmount = paymentsByBill
            .SelectMany(static pair => pair.Value)
            .Where(p => p.Status == PaymentStatus.Completed)
            .Where(p => IsWithinRange(p.PaymentDateUtc, filter.StartDateUtc, filter.EndDateUtc))
            .Sum(p => p.Amount);

        TotalBilledAmount = filteredBills.Sum(b => b.Amount);
        VarianceAmount = Math.Max(0m, TotalBilledAmount - CollectedAmount);

        _allRevenueRows.Clear();
        _allRevenueRows.AddRange(filteredBills
            .GroupBy(b => ResolveGroupingAnchor(b.CreatedAtUtc, GroupBySelection))
            .Select(group => new ReportRevenueRowItem
            {
                AnchorDateUtc = group.Key,
                Period = FormatPeriod(group.Key, GroupBySelection),
                Bills = group.Count(),
                Paid = group.Count(x => x.Status == BillStatus.Paid),
                Unpaid = group.Count(x => x.Status != BillStatus.Paid),
                Revenue = group.Sum(x => x.Amount - x.Balance)
            })
            .OrderBy(x => x.AnchorDateUtc));
    }

    private async Task LoadIssueReportAsync(ReportFilterDto filter, CancellationToken cancellationToken)
    {
        var logs = await _auditService.GetLogsAsync(filter.StartDateUtc, filter.EndDateUtc, cancellationToken);
        var issueLogs = logs
            .Where(IsIssueLog)
            .OrderByDescending(x => x.TimestampUtc)
            .ToList();

        TotalIssues = issueLogs.Count;
        PriorityIssues = issueLogs.Count(x => ContainsAny(BuildAuditBlob(x), "priority", "urgent", "critical"));
        CompletedIssues = issueLogs.Count(x => ContainsAny(BuildAuditBlob(x), "resolved", "completed", "closed"));

        IssueStatusSummary = issueLogs.Count == 0
            ? "No issue data for the selected range."
            : BuildIssueStatusSummary(issueLogs);

        IssueCategorySummary = issueLogs.Count == 0
            ? "No categorized issues found."
            : BuildIssueCategorySummary(issueLogs);

        RecentIssueSubmissions.Clear();
        foreach (var line in issueLogs
                     .Take(5)
                     .Select(log => $"{log.TimestampUtc:yyyy-MM-dd}  {ExtractAuditSummary(log)}"))
        {
            RecentIssueSubmissions.Add(line);
        }

        _allIssueTimelineRows.Clear();
        _allIssueTimelineRows.AddRange(issueLogs
            .GroupBy(x => x.TimestampUtc.Date)
            .OrderBy(x => x.Key)
            .Select(group => new ReportIssueTimelineRowItem
            {
                DateUtc = group.Key,
                DateDisplay = group.Key.ToString("yyyy-MM-dd"),
                IssueCount = group.Count()
            }));
    }

    private async Task LoadPrintableReportAsync(ReportFilterDto filter, CancellationToken cancellationToken)
    {
        var bills = await _billingService.GetBillsAsync(cancellationToken);
        var filteredBills = bills
            .Where(b => IsWithinRange(b.CreatedAtUtc, filter.StartDateUtc, filter.EndDateUtc))
            .Where(b => !filter.CustomerId.HasValue || b.CustomerId == filter.CustomerId.Value)
            .ToList();

        var customers = await _customerService.SearchCustomersAsync(null, cancellationToken);
        var filteredCustomers = customers
            .Where(c => IsWithinRange(c.CreatedAtUtc, filter.StartDateUtc, filter.EndDateUtc))
            .ToList();

        var logs = await _auditService.GetLogsAsync(filter.StartDateUtc, filter.EndDateUtc, cancellationToken);
        var paymentsByBill = await LoadPaymentsByBillAsync(filteredBills, cancellationToken);
        var totalCollected = paymentsByBill
            .SelectMany(static pair => pair.Value)
            .Where(p => p.Status == PaymentStatus.Completed)
            .Where(p => IsWithinRange(p.PaymentDateUtc, filter.StartDateUtc, filter.EndDateUtc))
            .Sum(p => p.Amount);

        var printRows = new List<ReportPrintableMetricRowItem>
        {
            new() { Metric = "Total billed", Value = $"P{filteredBills.Sum(x => x.Amount):N2}", Notes = "Invoices generated in period" },
            new() { Metric = "Total collected", Value = $"P{totalCollected:N2}", Notes = "Paid receipts" },
            new() { Metric = "Bills created", Value = filteredBills.Count.ToString(CultureInfo.InvariantCulture), Notes = "Billing records counted" },
            new() { Metric = "New customers", Value = filteredCustomers.Count.ToString(CultureInfo.InvariantCulture), Notes = "Registrations added" },
            new() { Metric = "Issue reports", Value = logs.Count(IsIssueLog).ToString(CultureInfo.InvariantCulture), Notes = "Filed by teams" },
            new() { Metric = "Meter replacements", Value = logs.Count(x => ContainsAny(BuildAuditBlob(x), "meter", "replace")).ToString(CultureInfo.InvariantCulture), Notes = "Recorded in audits" },
            new() { Metric = "Meter damages", Value = logs.Count(x => ContainsAny(BuildAuditBlob(x), "meter", "damage")).ToString(CultureInfo.InvariantCulture), Notes = "Damage-related actions" },
            new() { Metric = "Disconnections", Value = logs.Count(x => ContainsAny(BuildAuditBlob(x), "disconnect")).ToString(CultureInfo.InvariantCulture), Notes = "Performed actions" },
            new() { Metric = "Reconnections", Value = logs.Count(x => ContainsAny(BuildAuditBlob(x), "reconnect")).ToString(CultureInfo.InvariantCulture), Notes = "Restored accounts" }
        };

        _allPrintableRows.Clear();
        _allPrintableRows.AddRange(printRows);
    }

    private async Task<ReportFilterDto?> BuildFilterAsync(CancellationToken cancellationToken)
    {
        Guid? customerId = null;

        if (IsCustomerPaymentMode && !string.IsNullOrWhiteSpace(CustomerFilterText))
        {
            customerId = await ResolveCustomerIdAsync(cancellationToken);
            if (!customerId.HasValue)
            {
                await _dialogService.AlertAsync("Filter", "No customer matched the filter text. Showing all customers instead.");
            }
        }

        return new ReportFilterDto
        {
            StartDateUtc = StartDateUtc.Date,
            EndDateUtc = EndDateUtc.Date.AddDays(1).AddTicks(-1),
            CustomerId = customerId,
            IncludeOverdueOnly = false
        };
    }

    private async Task<Guid?> ResolveCustomerIdAsync(CancellationToken cancellationToken)
    {
        var text = CustomerFilterText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (Guid.TryParse(text, out var customerId))
        {
            return customerId;
        }

        var customers = await _customerService.SearchCustomersAsync(text, cancellationToken);
        if (customers.Count == 0)
        {
            return null;
        }

        if (customers.Count > 1)
        {
            StatusMessage = $"Multiple matches found for '{text}'. Using {customers[0].Name}.";
        }

        return customers[0].Id;
    }

    private async Task<Dictionary<Guid, IReadOnlyList<Payment>>> LoadPaymentsByBillAsync(
        IReadOnlyList<Bill> bills,
        CancellationToken cancellationToken)
    {
        if (bills.Count == 0)
        {
            return [];
        }

        var tasks = bills
            .Select(async bill => new
            {
                bill.Id,
                Payments = await _paymentService.GetPaymentsByBillAsync(bill.Id, cancellationToken)
            });

        var rows = await Task.WhenAll(tasks);
        return rows.ToDictionary(x => x.Id, x => x.Payments);
    }

    private void SetMode(ReportMode mode)
    {
        if (_selectedMode == mode)
        {
            return;
        }

        _selectedMode = mode;
        RaiseModeProperties();
        UpdateSortOptionsForMode();
        _ = _applyFiltersCommand.ExecuteAsync();
    }

    private void RaiseModeProperties()
    {
        RaisePropertyChanged(nameof(IsCustomerPaymentMode));
        RaisePropertyChanged(nameof(IsIssueMode));
        RaisePropertyChanged(nameof(IsPrintMode));
        RaisePropertyChanged(nameof(HeaderTitle));
        RaisePropertyChanged(nameof(HeaderSubtitle));
        RaisePropertyChanged(nameof(CustomerPaymentActionBackground));
        RaisePropertyChanged(nameof(CustomerPaymentActionTextColor));
        RaisePropertyChanged(nameof(IssueActionBackground));
        RaisePropertyChanged(nameof(IssueActionTextColor));
        RaisePropertyChanged(nameof(PrintActionBackground));
        RaisePropertyChanged(nameof(PrintActionTextColor));
        RaisePropertyChanged(nameof(SearchPlaceholder));
    }

    private void UpdateSortOptionsForMode()
    {
        _suppressSearchSortRefresh = true;
        try
        {
            SortOptions.Clear();
            var options = _selectedMode switch
            {
                ReportMode.Issues => new[] { "Date", "Issues" },
                ReportMode.Printables => new[] { "Metric", "Value" },
                _ => new[] { "Period", "Bills", "Paid", "Unpaid", "Revenue" }
            };

            foreach (var option in options)
            {
                SortOptions.Add(option);
            }

            if (!SortOptions.Contains(SelectedSortColumn))
            {
                SelectedSortColumn = SortOptions[0];
            }
        }
        finally
        {
            _suppressSearchSortRefresh = false;
        }

        ApplyCurrentModeSearchSort();
    }

    private void ApplyCurrentModeSearchSort()
    {
        switch (_selectedMode)
        {
            case ReportMode.Issues:
                ApplyIssueSearchSort();
                break;
            case ReportMode.Printables:
                ApplyPrintableSearchSort();
                break;
            default:
                ApplyRevenueSearchSort();
                break;
        }
    }

    private void ApplyRevenueSearchSort()
    {
        IEnumerable<ReportRevenueRowItem> query = _allRevenueRows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(x =>
                x.Period.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.RevenueDisplay.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedSortColumn switch
        {
            "Bills" => query.OrderBy(x => x.Bills),
            "Paid" => query.OrderBy(x => x.Paid),
            "Unpaid" => query.OrderBy(x => x.Unpaid),
            "Revenue" => query.OrderBy(x => x.Revenue),
            _ => query.OrderBy(x => x.AnchorDateUtc)
        };

        if (string.Equals(SelectedSortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Reverse();
        }

        RevenueBreakdownRows.Clear();
        foreach (var item in query.Select((row, index) => new ReportRevenueRowItem
                 {
                     AnchorDateUtc = row.AnchorDateUtc,
                     Period = row.Period,
                     Bills = row.Bills,
                     Paid = row.Paid,
                     Unpaid = row.Unpaid,
                     Revenue = row.Revenue,
                     RowBackground = index % 2 == 0 ? "#F9FBFE" : "#F2F6FC"
                 }))
        {
            RevenueBreakdownRows.Add(item);
        }

        RaisePropertyChanged(nameof(HasRevenueRows));
    }

    private void ApplyIssueSearchSort()
    {
        IEnumerable<ReportIssueTimelineRowItem> query = _allIssueTimelineRows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(x =>
                x.DateDisplay.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.IssueCount.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = string.Equals(SelectedSortColumn, "Issues", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(x => x.IssueCount)
            : query.OrderBy(x => x.DateUtc);

        if (string.Equals(SelectedSortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Reverse();
        }

        IssueTimelineRows.Clear();
        foreach (var item in query.Select((row, index) => new ReportIssueTimelineRowItem
                 {
                     DateUtc = row.DateUtc,
                     DateDisplay = row.DateDisplay,
                     IssueCount = row.IssueCount,
                     RowBackground = index % 2 == 0 ? "#F9FBFE" : "#F2F6FC"
                 }))
        {
            IssueTimelineRows.Add(item);
        }

        RaisePropertyChanged(nameof(HasIssueTimelineRows));
    }

    private void ApplyPrintableSearchSort()
    {
        IEnumerable<ReportPrintableMetricRowItem> query = _allPrintableRows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(x =>
                x.Metric.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Value.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Notes.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = string.Equals(SelectedSortColumn, "Value", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(x => ParseSortableValue(x.Value))
            : query.OrderBy(x => x.Metric);

        if (string.Equals(SelectedSortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Reverse();
        }

        PrintableMetricRows.Clear();
        foreach (var item in query.Select((row, index) => new ReportPrintableMetricRowItem
                 {
                     Metric = row.Metric,
                     Value = row.Value,
                     Notes = row.Notes,
                     RowBackground = index % 2 == 0 ? "#F9FBFE" : "#F2F6FC"
                 }))
        {
            PrintableMetricRows.Add(item);
        }

        RaisePropertyChanged(nameof(HasPrintableRows));
    }

    private static decimal ParseSortableValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var cleaned = value.Replace("P", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(",", string.Empty)
            .Trim();
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    private void SetQuickRange(int days)
    {
        EndDateUtc = DateTime.UtcNow.Date;
        StartDateUtc = EndDateUtc.AddDays(-days);
    }

    private void ResetFilters()
    {
        StartDateUtc = new DateTime(DateTime.UtcNow.Year, 1, 1);
        EndDateUtc = DateTime.UtcNow.Date;
        GroupBySelection = "Monthly";
        CustomerFilterText = string.Empty;
        SearchText = string.Empty;
        SelectedSortDirection = "Ascending";
        UpdateSortOptionsForMode();
        _ = _applyFiltersCommand.ExecuteAsync();
    }

    private async Task ExportCurrentReportAsync(bool printStyle, CancellationToken cancellationToken)
    {
        var filter = await BuildFilterAsync(cancellationToken);
        if (filter is null)
        {
            return;
        }

        string mode;
        string csv;
        switch (_selectedMode)
        {
            case ReportMode.Issues:
                mode = printStyle ? "issue-print" : "issue";
                csv = BuildIssueCsv();
                break;
            case ReportMode.Printables:
                mode = printStyle ? "printable-report" : "printable";
                csv = BuildPrintableCsv();
                break;
            default:
                mode = printStyle ? "customer-payment-print" : "customer-payment";
                csv = await _reportService.GeneratePaymentHistoryCsvAsync(filter, cancellationToken);
                break;
        }

        var path = await _reportFileWriter.WriteCsvAsync(mode, csv);
        LastExportPath = path;
        LastExportType = mode;
        LastExportedAtUtc = DateTime.UtcNow;
        LastExportRowCount = CountRows(csv);
        StatusMessage = printStyle ? "Printable report generated." : "Report exported.";

        await _dialogService.AlertAsync("Reports", $"CSV exported to {path}");
    }

    private async Task ExportSingleModeCsvAsync(string mode, CancellationToken cancellationToken)
    {
        var filter = await BuildFilterAsync(cancellationToken);
        if (filter is null)
        {
            return;
        }

        var csv = mode switch
        {
            "revenue" => await _reportService.GenerateRevenueReportCsvAsync(filter, cancellationToken),
            "overdue" => await _reportService.GenerateOverdueReportCsvAsync(filter, cancellationToken),
            _ => await _reportService.GeneratePaymentHistoryCsvAsync(filter, cancellationToken)
        };

        var path = await _reportFileWriter.WriteCsvAsync(mode, csv);
        LastExportPath = path;
        LastExportType = mode;
        LastExportedAtUtc = DateTime.UtcNow;
        LastExportRowCount = CountRows(csv);
        StatusMessage = "Report exported.";
        await _dialogService.AlertAsync("Reports", $"CSV exported to {path}");
    }

    private string BuildIssueCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Issues");
        foreach (var row in IssueTimelineRows)
        {
            builder.AppendLine($"{row.DateDisplay},{row.IssueCount}");
        }

        return builder.ToString();
    }

    private string BuildPrintableCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Metric,Value,Notes");
        foreach (var row in PrintableMetricRows)
        {
            builder.AppendLine($"{EscapeCsv(row.Metric)},{EscapeCsv(row.Value)},{EscapeCsv(row.Notes)}");
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        if (escaped.IndexOfAny([',', '"', '\n', '\r']) >= 0)
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }

    private static int CountRows(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return 0;
        }

        var rowCount = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length;
        return rowCount > 0 ? rowCount - 1 : 0;
    }

    private static DateTime ResolveGroupingAnchor(DateTime value, string groupBy)
    {
        var utcDate = value.Date;
        return groupBy switch
        {
            "Daily" => utcDate,
            "Weekly" => utcDate.AddDays(-((int)utcDate.DayOfWeek + 6) % 7),
            _ => new DateTime(utcDate.Year, utcDate.Month, 1)
        };
    }

    private static string FormatPeriod(DateTime value, string groupBy)
    {
        return groupBy switch
        {
            "Daily" => value.ToString("yyyy-MM-dd"),
            "Weekly" => $"{value:yyyy-MM-dd} wk",
            _ => value.ToString("MMM yyyy")
        };
    }

    private static bool IsWithinRange(DateTime value, DateTime? startUtc, DateTime? endUtc)
    {
        if (startUtc.HasValue && value < startUtc.Value)
        {
            return false;
        }

        if (endUtc.HasValue && value > endUtc.Value)
        {
            return false;
        }

        return true;
    }

    private static bool IsIssueLog(AuditLog log)
    {
        return ContainsAny(BuildAuditBlob(log), "issue", "complaint", "ticket", "notice");
    }

    private static string BuildIssueStatusSummary(IReadOnlyList<AuditLog> logs)
    {
        var created = logs.Count(x => x.ActionType == AuditActionType.Create);
        var updated = logs.Count(x => x.ActionType == AuditActionType.Update);
        var closed = logs.Count(x => ContainsAny(BuildAuditBlob(x), "resolved", "closed", "completed"));
        return $"Created {created} | Updated {updated} | Closed {closed}";
    }

    private static string BuildIssueCategorySummary(IReadOnlyList<AuditLog> logs)
    {
        var topCategories = logs
            .GroupBy(x => string.IsNullOrWhiteSpace(x.EntityName) ? "Unknown" : x.EntityName)
            .OrderByDescending(x => x.Count())
            .Take(3)
            .Select(x => $"{x.Key} ({x.Count()})");

        return string.Join("  |  ", topCategories);
    }

    private static string ExtractAuditSummary(AuditLog log)
    {
        if (TryExtractContextField(log.Context, "Context", out var summary) && !string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        return $"{log.Username ?? "Unknown user"} {log.ActionType.ToString().ToLowerInvariant()} on {log.EntityName}";
    }

    private static string BuildAuditBlob(AuditLog log)
    {
        return string.Join(' ', [
            log.EntityName,
            log.EntityId,
            log.Username,
            log.Context,
            log.OldValuesJson,
            log.NewValuesJson
        ]).ToLowerInvariant();
    }

    private static bool ContainsAny(string source, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (source.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractContextField(string? contextJson, string propertyName, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(propertyName, out var field))
            {
                return false;
            }

            value = field.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
