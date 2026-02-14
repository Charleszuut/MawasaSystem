using Microsoft.Extensions.DependencyInjection;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.Shell;

public partial class AppShell : Microsoft.Maui.Controls.Shell
{
    private readonly AppStateStore _stateStore;
    private readonly IRbacService _rbacService;

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

    private void ApplyAccessPolicies()
    {
        var session = _stateStore.Session;
        var isAuthenticated = session is not null;

        FlyoutBehavior = isAuthenticated
            ? FlyoutBehavior.Flyout
            : FlyoutBehavior.Disabled;

        DashboardItem.IsVisible = isAuthenticated;
        BillingItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "billing.manage");
        PaymentsItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "payments.manage");
        CustomersItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "customers.manage");
        ReportsItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "reports.export");
        AuditItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "audit.view");
        SettingsItem.IsVisible = isAuthenticated && _rbacService.HasPermission(session, "users.manage");
    }
}
