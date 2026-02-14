using System.Collections.ObjectModel;
using System.Globalization;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Presentation.ViewModels.Core;
using MawasaProject.Presentation.ViewModels.Models;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class DashboardViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly AsyncCommand _refreshCommand;

    private DateTime _startDateUtc = DateTime.UtcNow.AddMonths(-11);
    private DateTime _endDateUtc = DateTime.UtcNow;
    private decimal _totalRevenue;
    private decimal _outstandingBalance;
    private int _totalBills;
    private int _overdueBills;
    private int _pendingBills;
    private int _totalCustomers;
    private decimal _averageMonthlyRevenue;
    private decimal _monthlyRevenueChangePercentage;
    private double _overdueRate;

    public DashboardViewModel()
        : this(App.Services.GetRequiredService<IDashboardService>())
    {
    }

    public DashboardViewModel(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        RevenueSeries = [];
        _refreshCommand = new AsyncCommand(async () => await RefreshAsync());
    }

    public DateTime StartDateUtc
    {
        get => _startDateUtc;
        set => SetProperty(ref _startDateUtc, value);
    }

    public DateTime EndDateUtc
    {
        get => _endDateUtc;
        set => SetProperty(ref _endDateUtc, value);
    }

    public decimal TotalRevenue
    {
        get => _totalRevenue;
        set => SetProperty(ref _totalRevenue, value);
    }

    public decimal OutstandingBalance
    {
        get => _outstandingBalance;
        set => SetProperty(ref _outstandingBalance, value);
    }

    public int TotalBills
    {
        get => _totalBills;
        set => SetProperty(ref _totalBills, value);
    }

    public int OverdueBills
    {
        get => _overdueBills;
        set => SetProperty(ref _overdueBills, value);
    }

    public int PendingBills
    {
        get => _pendingBills;
        set => SetProperty(ref _pendingBills, value);
    }

    public int TotalCustomers
    {
        get => _totalCustomers;
        set => SetProperty(ref _totalCustomers, value);
    }

    public decimal AverageMonthlyRevenue
    {
        get => _averageMonthlyRevenue;
        set => SetProperty(ref _averageMonthlyRevenue, value);
    }

    public decimal MonthlyRevenueChangePercentage
    {
        get => _monthlyRevenueChangePercentage;
        set => SetProperty(ref _monthlyRevenueChangePercentage, value);
    }

    public double OverdueRate
    {
        get => _overdueRate;
        set => SetProperty(ref _overdueRate, value);
    }

    public ObservableCollection<DashboardRevenuePoint> RevenueSeries { get; }

    public AsyncCommand RefreshCommand => _refreshCommand;

    private async Task RefreshAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "Refreshing dashboard analytics...";
            var summary = await _dashboardService.GetSummaryAsync(StartDateUtc, EndDateUtc);

            TotalRevenue = summary.TotalRevenue;
            OutstandingBalance = summary.OutstandingBalance;
            TotalBills = summary.TotalBills;
            OverdueBills = summary.OverdueBills;
            PendingBills = summary.PendingBills;
            TotalCustomers = summary.TotalCustomers;

            var series = summary.RevenueSeries.OrderBy(x => x.Year).ThenBy(x => x.Month).ToArray();
            var maxRevenue = series.Length == 0 ? 0m : series.Max(x => x.Revenue);

            RevenueSeries.Clear();
            foreach (var point in series)
            {
                var monthLabel = new DateTime(point.Year, point.Month, 1).ToString("MMM yy", CultureInfo.InvariantCulture);
                RevenueSeries.Add(new DashboardRevenuePoint
                {
                    Label = monthLabel,
                    Revenue = point.Revenue,
                    RelativeHeight = maxRevenue <= 0m ? 0d : (double)(point.Revenue / maxRevenue)
                });
            }

            AverageMonthlyRevenue = series.Length == 0 ? 0m : series.Average(x => x.Revenue);
            MonthlyRevenueChangePercentage = CalculateMonthOverMonthChange(series);
            OverdueRate = TotalBills == 0 ? 0d : (double)OverdueBills / TotalBills;
            StatusMessage = "Dashboard updated.";
        });
    }

    private static decimal CalculateMonthOverMonthChange(IReadOnlyList<MonthlyRevenuePointDto> series)
    {
        if (series.Count < 2)
        {
            return 0m;
        }

        var latest = series[^1].Revenue;
        var previous = series[^2].Revenue;
        if (previous == 0m)
        {
            return latest > 0m ? 100m : 0m;
        }

        return Math.Round(((latest - previous) / previous) * 100m, 2);
    }
}
