using Microsoft.Extensions.DependencyInjection;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.Shell;

public partial class AppShell : Microsoft.Maui.Controls.Shell
{
    private readonly AppStateStore _stateStore;
    private readonly IRbacService _rbacService;
    private static readonly Color ActiveMenuColor = Color.FromArgb("#22ABE8");
    private static readonly Color InactiveMenuColor = Colors.Transparent;

    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(RouteMap.Backup, typeof(Views.Pages.BackupPage));
        Routing.RegisterRoute(RouteMap.PrinterSettings, typeof(Views.Pages.PrinterSettingsPage));
        Routing.RegisterRoute(RouteMap.PrintQueue, typeof(Views.Pages.PrintQueuePage));
        Routing.RegisterRoute(RouteMap.Receipt, typeof(Views.Pages.ReceiptPage));
        Routing.RegisterRoute(RouteMap.Invoice, typeof(Views.Pages.InvoicePage));

        _stateStore = App.Services.GetRequiredService<AppStateStore>();
        _rbacService = App.Services.GetRequiredService<IRbacService>();
        Navigated += OnShellNavigated;

        _stateStore.PropertyChanged += OnStateChanged;
        ApplyAccessPolicies();

        if (_stateStore.Session is null)
        {
            Dispatcher.Dispatch(() =>
            {
                _ = GoToAsync(RouteMap.LoginRoot);
            });
        }
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AppStateStore.Session), StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            ApplyAccessPolicies();
            if (_stateStore.Session is null)
            {
                _ = GoToAsync(RouteMap.LoginRoot);
            }
        });
    }

    private async void OnDashboardTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.DashboardHome);

    private async void OnBillingTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.BillingHome);

    private async void OnPaymentsTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.PaymentsHome);

    private async void OnCustomersTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.CustomersHome);

    private async void OnReportsTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.ReportsHome);

    private async void OnAuditTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.AuditHome);

    private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await NavigateToAsync(RouteMap.SettingsHome);

    private async Task NavigateToAsync(string route)
    {
        if (_stateStore.Session is null)
        {
            return;
        }

        await GoToAsync(route);
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateSidebarSelection();
    }

    private void ApplyAccessPolicies()
    {
        var session = _stateStore.Session;
        var isAuthenticated = session is not null;

        FlyoutBehavior = isAuthenticated
            ? FlyoutBehavior.Locked
            : FlyoutBehavior.Disabled;

        DashboardItem.IsVisible = isAuthenticated;
        BillingItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "billing.manage");
        PaymentsItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "payments.manage");
        CustomersItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "customers.manage");
        ReportsItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "reports.export");
        AuditItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "audit.view");
        SettingsItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "users.manage");

        AdminMenuLabel.IsVisible = isAuthenticated;
        DashboardNavItem.IsVisible = DashboardItem.IsVisible;
        BillingNavItem.IsVisible = BillingItem.IsVisible;
        PaymentsNavItem.IsVisible = PaymentsItem.IsVisible;
        CustomersNavItem.IsVisible = CustomersItem.IsVisible;
        ReportsNavItem.IsVisible = ReportsItem.IsVisible;
        AuditNavItem.IsVisible = AuditItem.IsVisible;
        SettingsNavItem.IsVisible = SettingsItem.IsVisible;

        UpdateSidebarSelection();
    }

    private void UpdateSidebarSelection()
    {
        var route = CurrentState?.Location?.OriginalString ?? string.Empty;

        SetMenuState(DashboardNavItem, route.Contains("//dashboard", StringComparison.OrdinalIgnoreCase));
        SetMenuState(BillingNavItem, route.Contains("//billing", StringComparison.OrdinalIgnoreCase));
        SetMenuState(PaymentsNavItem, route.Contains("//payments", StringComparison.OrdinalIgnoreCase));
        SetMenuState(CustomersNavItem, route.Contains("//customers", StringComparison.OrdinalIgnoreCase));
        SetMenuState(ReportsNavItem, route.Contains("//reports", StringComparison.OrdinalIgnoreCase));
        SetMenuState(AuditNavItem, route.Contains("//audit", StringComparison.OrdinalIgnoreCase));

        var settingsActive = route.Contains("//settings", StringComparison.OrdinalIgnoreCase)
            || route.Contains(RouteMap.Backup, StringComparison.OrdinalIgnoreCase)
            || route.Contains(RouteMap.PrinterSettings, StringComparison.OrdinalIgnoreCase)
            || route.Contains(RouteMap.PrintQueue, StringComparison.OrdinalIgnoreCase)
            || route.Contains(RouteMap.Receipt, StringComparison.OrdinalIgnoreCase)
            || route.Contains(RouteMap.Invoice, StringComparison.OrdinalIgnoreCase);
        SetMenuState(SettingsNavItem, settingsActive);
    }

    private static void SetMenuState(Border menuItem, bool active)
    {
        menuItem.BackgroundColor = active ? ActiveMenuColor : InactiveMenuColor;

        if (menuItem.Content is Label label)
        {
            label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }
    }
}
