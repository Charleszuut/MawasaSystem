using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class DashboardViewModel(IDashboardService dashboardService) : BaseViewModel
{
    private decimal _totalRevenue;
    private decimal _outstandingBalance;
    private int _overdueBills;
    private int _pendingBills;
    private int _totalCustomers;

    public DashboardViewModel() : this(App.Services.GetRequiredService<IDashboardService>())
    {
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

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var summary = await dashboardService.GetSummaryAsync(DateTime.UtcNow.AddMonths(-12), DateTime.UtcNow);
        TotalRevenue = summary.TotalRevenue;
        OutstandingBalance = summary.OutstandingBalance;
        OverdueBills = summary.OverdueBills;
        PendingBills = summary.PendingBills;
        TotalCustomers = summary.TotalCustomers;
    }));
}
