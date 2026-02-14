using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class AuditViewModel : BaseViewModel
{
    private const int PageSize = 20;
    private readonly IAuditService _auditService;

    private readonly List<AuditLogRowItem> _allRows = [];
    private readonly List<AuditLogRowItem> _filteredRows = [];

    private readonly AsyncCommand _refreshCommand;
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;
    private readonly RelayCommand _closeDetailsCommand;
    private readonly RelayCommandOfT<AuditLogRowItem> _openDetailsCommand;

    private string _searchText = string.Empty;
    private string _selectedSortColumn = "Time";
    private string _selectedSortDirection = "Descending";
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _filteredCount;

    private bool _isDetailsVisible;
    private string _detailsPerformedBy = "--";
    private string _detailsWhen = "--";
    private string _detailsModule = "--";
    private string _detailsAction = "--";
    private string _detailsSummary = "--";
    private string _detailsTarget = "--";

    public AuditViewModel()
        : this(App.Services.GetRequiredService<IAuditService>())
    {
    }

    public AuditViewModel(IAuditService auditService)
    {
        _auditService = auditService;

        Logs = [];
        DetailMetadata = [];

        _refreshCommand = new AsyncCommand(async () => await RunBusyAsync(LoadLogsAsync));
        _previousPageCommand = new RelayCommand(MovePreviousPage, () => CanGoPreviousPage);
        _nextPageCommand = new RelayCommand(MoveNextPage, () => CanGoNextPage);
        _closeDetailsCommand = new RelayCommand(CloseDetails);
        _openDetailsCommand = new RelayCommandOfT<AuditLogRowItem>(OpenDetails);
    }

    public ObservableCollection<AuditLogRowItem> Logs { get; }

    public ObservableCollection<AuditMetadataRowItem> DetailMetadata { get; }

    public IReadOnlyList<string> SortColumns { get; } = ["Time", "User", "Module", "Action"];

    public IReadOnlyList<string> SortDirections { get; } = ["Ascending", "Descending"];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            ApplyFilters(resetToFirstPage: true);
        }
    }

    public string SelectedSortColumn
    {
        get => _selectedSortColumn;
        set
        {
            if (!SetProperty(ref _selectedSortColumn, value))
            {
                return;
            }

            ApplyFilters(resetToFirstPage: true);
        }
    }

    public string SelectedSortDirection
    {
        get => _selectedSortDirection;
        set
        {
            if (!SetProperty(ref _selectedSortDirection, value))
            {
                return;
            }

            ApplyFilters(resetToFirstPage: true);
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!SetProperty(ref _currentPage, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(PageSummary));
            RaisePropertyChanged(nameof(ResultSummary));
            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            _previousPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (!SetProperty(ref _totalPages, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(PageSummary));
            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            _previousPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CanGoPreviousPage => CurrentPage > 1;

    public bool CanGoNextPage => CurrentPage < TotalPages;

    public string EntriesSummary => $"{_filteredCount} entries";

    public string ResultSummary
    {
        get
        {
            if (_filteredCount == 0)
            {
                return "Showing 0 of 0 results";
            }

            var start = ((CurrentPage - 1) * PageSize) + 1;
            var end = Math.Min(CurrentPage * PageSize, _filteredCount);
            return $"Showing {start} to {end} of {_filteredCount} results";
        }
    }

    public string PageSummary => $"Page {CurrentPage} of {TotalPages}";

    public bool IsDetailsVisible
    {
        get => _isDetailsVisible;
        private set => SetProperty(ref _isDetailsVisible, value);
    }

    public string DetailsPerformedBy
    {
        get => _detailsPerformedBy;
        private set => SetProperty(ref _detailsPerformedBy, value);
    }

    public string DetailsWhen
    {
        get => _detailsWhen;
        private set => SetProperty(ref _detailsWhen, value);
    }

    public string DetailsModule
    {
        get => _detailsModule;
        private set => SetProperty(ref _detailsModule, value);
    }

    public string DetailsAction
    {
        get => _detailsAction;
        private set => SetProperty(ref _detailsAction, value);
    }

    public string DetailsSummary
    {
        get => _detailsSummary;
        private set => SetProperty(ref _detailsSummary, value);
    }

    public string DetailsTarget
    {
        get => _detailsTarget;
        private set => SetProperty(ref _detailsTarget, value);
    }

    public AsyncCommand RefreshCommand => _refreshCommand;

    public RelayCommand PreviousPageCommand => _previousPageCommand;

    public RelayCommand NextPageCommand => _nextPageCommand;

    public RelayCommand CloseDetailsCommand => _closeDetailsCommand;

    public RelayCommandOfT<AuditLogRowItem> OpenDetailsCommand => _openDetailsCommand;

    private async Task LoadLogsAsync(CancellationToken cancellationToken)
    {
        var items = await _auditService.GetLogsAsync(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, cancellationToken);
        _allRows.Clear();

        foreach (var item in items.OrderByDescending(x => x.TimestampUtc))
        {
            _allRows.Add(MapRow(item));
        }

        ApplyFilters(resetToFirstPage: true);
        StatusMessage = $"Loaded {_allRows.Count} audit log entries.";
    }

    private void ApplyFilters(bool resetToFirstPage)
    {
        IEnumerable<AuditLogRowItem> query = _allRows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(row =>
                row.TimeDisplay.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.UserDisplay.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.ModuleDisplay.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.ActionDisplay.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.DescriptionDisplay.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedSortColumn switch
        {
            "User" => query.OrderBy(x => x.UserDisplay),
            "Module" => query.OrderBy(x => x.ModuleDisplay),
            "Action" => query.OrderBy(x => x.ActionDisplay),
            _ => query.OrderBy(x => x.TimeUtc)
        };

        if (string.Equals(SelectedSortDirection, "Descending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Reverse();
        }

        _filteredRows.Clear();
        _filteredRows.AddRange(query);
        _filteredCount = _filteredRows.Count;

        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        TotalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(1, _filteredCount) / (double)PageSize));
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        RebuildCurrentPageRows();
        RaisePropertyChanged(nameof(ResultSummary));
        RaisePropertyChanged(nameof(EntriesSummary));
    }

    private void RebuildCurrentPageRows()
    {
        Logs.Clear();
        if (_filteredRows.Count == 0)
        {
            return;
        }

        var skip = (CurrentPage - 1) * PageSize;
        foreach (var row in _filteredRows.Skip(skip).Take(PageSize).Select((item, index) => new AuditLogRowItem
                 {
                     Source = item.Source,
                     TimeUtc = item.TimeUtc,
                     TimeDisplay = item.TimeDisplay,
                     UserDisplay = item.UserDisplay,
                     ModuleDisplay = item.ModuleDisplay,
                     ActionDisplay = item.ActionDisplay,
                     DescriptionDisplay = item.DescriptionDisplay,
                     RowBackground = index % 2 == 0 ? "#F9FBFE" : "#F2F6FC"
                 }))
        {
            Logs.Add(row);
        }
    }

    private void MovePreviousPage()
    {
        if (!CanGoPreviousPage)
        {
            return;
        }

        CurrentPage--;
        RebuildCurrentPageRows();
    }

    private void MoveNextPage()
    {
        if (!CanGoNextPage)
        {
            return;
        }

        CurrentPage++;
        RebuildCurrentPageRows();
    }

    private void OpenDetails(AuditLogRowItem? row)
    {
        if (row is null)
        {
            return;
        }

        var source = row.Source;

        DetailsPerformedBy = string.IsNullOrWhiteSpace(source.Username) ? "System" : source.Username;
        DetailsWhen = source.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        DetailsModule = row.ModuleDisplay;
        DetailsAction = row.ActionDisplay;
        DetailsSummary = row.DescriptionDisplay;
        DetailsTarget = $"Type: {source.EntityName}  |  ID: {source.EntityId ?? "--"}";

        DetailMetadata.Clear();

        AddMetadata("IP", source.DeviceIpAddress);
        AddMetadata("Entity", source.EntityName);
        AddMetadata("Entity ID", source.EntityId);

        foreach (var (key, value) in ParseContext(source.Context))
        {
            AddMetadata(key, value);
        }

        AddMetadata("Old value", source.OldValuesJson);
        AddMetadata("New value", source.NewValuesJson);

        IsDetailsVisible = true;
    }

    private void CloseDetails()
    {
        IsDetailsVisible = false;
    }

    private void AddMetadata(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        DetailMetadata.Add(new AuditMetadataRowItem
        {
            Key = key,
            Value = value.Trim()
        });
    }

    private static AuditLogRowItem MapRow(AuditLog log)
    {
        return new AuditLogRowItem
        {
            Source = log,
            TimeUtc = log.TimestampUtc,
            TimeDisplay = log.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            UserDisplay = string.IsNullOrWhiteSpace(log.Username) ? "System" : log.Username,
            ModuleDisplay = string.IsNullOrWhiteSpace(log.EntityName) ? "General" : log.EntityName,
            ActionDisplay = log.ActionType.ToString().ToUpperInvariant(),
            DescriptionDisplay = BuildDescription(log)
        };
    }

    private static string BuildDescription(AuditLog log)
    {
        if (TryExtractContextField(log.Context, "Context", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return TrimToLength(value, 140);
        }

        if (!string.IsNullOrWhiteSpace(log.Context))
        {
            return TrimToLength(log.Context, 140);
        }

        return $"{log.Username ?? "User"} {log.ActionType.ToString().ToLowerInvariant()}";
    }

    private static IReadOnlyDictionary<string, string> ParseContext(string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var document = JsonDocument.Parse(context);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string> { ["Context"] = context };
            }

            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    output[property.Name] = value;
                }
            }

            return output;
        }
        catch
        {
            return new Dictionary<string, string> { ["Context"] = context };
        }
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
            if (!document.RootElement.TryGetProperty(propertyName, out var field))
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

    private static string TrimToLength(string value, int length)
    {
        if (value.Length <= length)
        {
            return value;
        }

        return value[..length];
    }
}
