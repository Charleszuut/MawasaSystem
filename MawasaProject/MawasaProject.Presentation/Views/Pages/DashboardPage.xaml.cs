using System.Text.Json;
using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class DashboardPage : ContentPage
{
    private DashboardViewModel ViewModel => (DashboardViewModel)BindingContext;
    private bool _revenueChartLoaded;
    private bool _analyticsChartLoaded;

    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<DashboardViewModel>();

        // Re-render chart whenever revenue series changes
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.RevenueSeries))
                _ = PushRevenueChartDataAsync();
            if (e.PropertyName == nameof(DashboardViewModel.CollectionRate) || 
                e.PropertyName == nameof(DashboardViewModel.TotalRevenue) || 
                e.PropertyName == nameof(DashboardViewModel.OutstandingBalance))
                _ = PushAnalyticsChartDataAsync();
        };

        ViewModel.RefreshCommand.Execute(null);
    }

    private async void OnChartNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _revenueChartLoaded = true;
        await PushRevenueChartDataAsync();
    }

    private async void OnAnalyticsChartNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _analyticsChartLoaded = true;
        await PushAnalyticsChartDataAsync();
    }

    private async Task PushRevenueChartDataAsync()
    {
        if (!_revenueChartLoaded) return;
        if (ViewModel.RevenueSeries.Count == 0) return;

        var labels = ViewModel.RevenueSeries.Select(p => p.Label).ToArray();
        var values = ViewModel.RevenueSeries.Select(p => (double)p.Revenue).ToArray();

        var json = JsonSerializer.Serialize(new { labels, values });
        // Escape single quotes for JS injection
        var escaped = json.Replace("'", "\\'");
        var js = $"updateChart('{escaped}')";

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await RevenueChartView.EvaluateJavaScriptAsync(js); }
            catch { /* WebView may not be ready yet */ }
        });
    }

    private async Task PushAnalyticsChartDataAsync()
    {
        if (!_analyticsChartLoaded) return;

        var collected = (double)ViewModel.TotalRevenue;
        var outstanding = (double)ViewModel.OutstandingBalance;
        var rateLabel = ViewModel.CollectionRate.ToString("P0");

        var json = JsonSerializer.Serialize(new { collected, outstanding, rateLabel });
        var escaped = json.Replace("'", "\\'");
        var js = $"updateChart('{escaped}')";

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await AnalyticsChartView.EvaluateJavaScriptAsync(js); }
            catch { /* WebView may not be ready yet */ }
        });
    }
}
