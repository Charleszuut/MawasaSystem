using Microsoft.Extensions.DependencyInjection;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Presentation.Diagnostics;
using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.Shell;

public partial class AppShell : Microsoft.Maui.Controls.Shell
{
    private readonly AppStateStore _stateStore;
    private readonly IRbacService _rbacService;
    private bool _isNavigatingAnimationRunning;
    private Page? _lastAnimatedPage;

    private static readonly Color ActiveBg      = Color.FromArgb("#1E7FC2");
    private static readonly Color InactiveBg    = Colors.Transparent;
    private static readonly Color SubActiveBg   = Color.FromArgb("#1E7FC2");
    private static readonly Color SubInactiveBg = Color.FromArgb("#0D2236");

    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(RouteMap.Backup,           typeof(Views.Pages.BackupPage));
        Routing.RegisterRoute(RouteMap.PrinterSettings,  typeof(Views.Pages.PrinterSettingsPage));
        Routing.RegisterRoute(RouteMap.PrintQueue,       typeof(Views.Pages.PrintQueuePage));
        Routing.RegisterRoute(RouteMap.Receipt,          typeof(Views.Pages.ReceiptPage));
        Routing.RegisterRoute(RouteMap.Invoice,          typeof(Views.Pages.InvoicePage));

        _stateStore  = App.Services.GetRequiredService<AppStateStore>();
        _rbacService = App.Services.GetRequiredService<IRbacService>();
        Navigated   += OnShellNavigated;

        _stateStore.PropertyChanged += OnStateChanged;
        ApplyAccessPolicies();

        if (_stateStore.Session is null)
            Dispatcher.Dispatch(() => _ = GoToAsync(RouteMap.LoginRoot));
    }

    // ──────────────────────────────────────────────────────────────
    // Session / access policy
    // ──────────────────────────────────────────────────────────────

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AppStateStore.Session), StringComparison.Ordinal))
            return;

        Dispatcher.Dispatch(() =>
        {
            ApplyAccessPolicies();
            var route = CurrentState?.Location?.OriginalString ?? string.Empty;
            if (_stateStore.Session is null)
            {
                _ = GoToAsync(RouteMap.LoginRoot);
                return;
            }
            if (route.Contains("login", StringComparison.OrdinalIgnoreCase))
                _ = GoToAsync(RouteMap.DashboardHome);
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Tap handlers
    // ──────────────────────────────────────────────────────────────

    private async void OnDashboardTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(DashboardNavBorder);
        await NavigateToAsync(RouteMap.DashboardHome);
    }

    private async void OnBillingTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(BillingNavBorder);
        await NavigateToAsync(RouteMap.BillingHome);
    }

    private async void OnPaymentsTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(PaymentsNavBorder);
        await NavigateToAsync(RouteMap.PaymentsHome);
    }

    private async void OnCustomersTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(CustomersNavBorder);
        await ToggleSubMenuAsync(CustomersActionMenu, CustomersChevron);
        await NavigateToAsync(RouteMap.CustomersManagementHome);
    }

    private async void OnCustomersManagementTapped(object? sender, TappedEventArgs e)
    {
        await PressSubItemAsync(CustomersManagementActionItem);
        await NavigateToAsync(RouteMap.CustomersManagementHome);
    }

    private async void OnCustomersRegisterTapped(object? sender, TappedEventArgs e)
    {
        await PressSubItemAsync(CustomersRegisterActionItem);
        await NavigateToAsync(RouteMap.CustomersRegisterHome);
    }

    private async void OnReportsTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(ReportsNavBorder);
        await ToggleSubMenuAsync(ReportsActionMenu, ReportsChevron);
        await NavigateToAsync(RouteMap.ReportsCustomerPaymentHome);
    }

    private async void OnReportsCustomerPaymentTapped(object? sender, TappedEventArgs e)
    {
        await PressSubItemAsync(ReportsPaymentsActionItem);
        await NavigateToAsync(RouteMap.ReportsCustomerPaymentHome);
    }

    private async void OnReportsIssueTapped(object? sender, TappedEventArgs e)
    {
        await PressSubItemAsync(ReportsIssueActionItem);
        await NavigateToAsync(RouteMap.ReportsIssueHome);
    }

    private async void OnReportsPrintTapped(object? sender, TappedEventArgs e)
    {
        await PressSubItemAsync(ReportsPrintActionItem);
        await NavigateToAsync(RouteMap.ReportsPrintHome);
    }

    private async void OnAuditTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(AuditNavBorder);
        await NavigateToAsync(RouteMap.AuditHome);
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await PressAnimateAsync(SettingsNavBorder);
        await NavigateToAsync(RouteMap.SettingsHome);
    }

    // ──────────────────────────────────────────────────────────────
    // Navigation
    // ──────────────────────────────────────────────────────────────

    private string _activeRouteOverride = "";

    private async Task NavigateToAsync(string route)
    {
        if (_stateStore.Session is null) return;
        _activeRouteOverride = route;
        try { await GoToAsync(route); }
        catch (Exception ex) { AppDiagnostics.LogException($"Navigation failed: {route}", ex); }
    }

    // ──────────────────────────────────────────────────────────────
    // Animation helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>Spring press for top-level nav items.</summary>
    private static async Task PressAnimateAsync(VisualElement element)
    {
        await element.ScaleToAsync(0.93, 70, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }

    private static readonly Color FlashColor = Color.FromArgb("#3DB1F5");

    /// <summary>Flash highlight for sub-menu items (press → bright → settle).</summary>
    private static async Task PressSubItemAsync(Border item)
    {
        var original = item.BackgroundColor;
        item.BackgroundColor = FlashColor;
        await item.ScaleToAsync(0.96, 60, Easing.CubicIn);
        await item.ScaleToAsync(1.0, 100, Easing.CubicOut);
        await Task.Delay(180);
        // Only reset if still showing the flash color (UpdateSidebarSelection may have set it already)
        if (item.BackgroundColor.ToArgbHex() == FlashColor.ToArgbHex())
            item.BackgroundColor = original;
    }

    /// <summary>Animated sub-menu expand / collapse.</summary>
    private static async Task ToggleSubMenuAsync(VisualElement menu, Label chevron)
    {
        if (!menu.IsVisible)
        {
            menu.Opacity = 0;
            menu.TranslationY = -8;
            menu.IsVisible = true;
            await Task.WhenAll(
                menu.FadeToAsync(1, 160, Easing.CubicOut),
                menu.TranslateToAsync(0, 0, 180, Easing.CubicOut));
            chevron.Text = "⌄";
        }
        else
        {
            await Task.WhenAll(
                menu.FadeToAsync(0, 120, Easing.CubicIn),
                menu.TranslateToAsync(0, -8, 140, Easing.CubicIn));
            menu.IsVisible = false;
            chevron.Text = "›";
        }
    }

    /// <summary>Page slide-in with scale on every navigation.</summary>
    private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateSidebarSelection();
        await AnimateCurrentPageAsync();
    }

    private async Task AnimateCurrentPageAsync()
    {
        if (_isNavigatingAnimationRunning) return;

        var page = CurrentPage;
        if (page is null || ReferenceEquals(page, _lastAnimatedPage)) return;

        var root = (page as ContentPage)?.Content as VisualElement ?? page as VisualElement;
        if (root is null) { _lastAnimatedPage = page; return; }

        _isNavigatingAnimationRunning = true;
        _lastAnimatedPage = page;

        try
        {
            root.AbortAnimation("ShellPageEnter");
            var origOpacity = root.Opacity;
            var origX       = root.TranslationX;
            root.Opacity      = 0;
            root.TranslationX = 22;
            root.Scale        = 0.98;

            await Task.WhenAll(
                root.FadeToAsync(origOpacity, 180U, Easing.CubicOut),
                root.TranslateToAsync(origX, root.TranslationY, 210U, Easing.CubicOut),
                root.ScaleToAsync(1.0, 210U, Easing.CubicOut));
        }
        finally { _isNavigatingAnimationRunning = false; }
    }

    // ──────────────────────────────────────────────────────────────
    // Sidebar state management
    // ──────────────────────────────────────────────────────────────

    private void ApplyAccessPolicies()
    {
        var session         = _stateStore.Session;
        var isAuthenticated = session is not null;

        FlyoutBehavior = isAuthenticated ? FlyoutBehavior.Locked : FlyoutBehavior.Disabled;

        DashboardItem.IsVisible  = isAuthenticated;
        BillingItem.IsVisible    = isAuthenticated && _rbacService.HasPermission(session, "billing.manage");
        PaymentsItem.IsVisible   = isAuthenticated && _rbacService.HasPermission(session, "payments.manage");
        CustomersItem.IsVisible  = isAuthenticated && _rbacService.HasPermission(session, "customers.manage");
        ReportsItem.IsVisible    = isAuthenticated && _rbacService.HasPermission(session, "reports.export");
        AuditItem.IsVisible      = isAuthenticated && _rbacService.HasPermission(session, "audit.view");
        SettingsItem.IsVisible   = isAuthenticated && _rbacService.HasPermission(session, "users.manage");

        // Show/hide nav panel items
        AdminMenuLabel.IsVisible          = isAuthenticated;
        DashboardNavItem.IsVisible        = DashboardItem.IsVisible;
        BillingNavItem.IsVisible          = BillingItem.IsVisible;
        PaymentsNavItem.IsVisible         = PaymentsItem.IsVisible;
        CustomersNavItem.IsVisible        = CustomersItem.IsVisible;
        CustomersActionMenu.IsVisible     = false;
        ReportsNavItem.IsVisible          = ReportsItem.IsVisible;
        ReportsActionMenu.IsVisible       = false;
        AuditNavItem.IsVisible            = AuditItem.IsVisible;
        SettingsNavItem.IsVisible         = SettingsItem.IsVisible;

        // Update role badge text based on session roles
        if (session is not null)
        {
            var role = session.Roles.FirstOrDefault();
            RoleLabel.Text = role.ToString().ToUpperInvariant() + " MENU";
            UserLabel.Text = session.Username;
        }

        UpdateSidebarSelection();
    }

    private void UpdateSidebarSelection()
    {
        var route = CurrentState?.Location?.OriginalString ?? string.Empty;

        // Preserve query string (like ?mode=register) since MAUI Shell sometimes strips it
        if (!string.IsNullOrEmpty(_activeRouteOverride))
        {
            var overrideBase = _activeRouteOverride.Split('?')[0];
            var routeBase = route.Split('?')[0];
            if (string.Equals(overrideBase, routeBase, StringComparison.OrdinalIgnoreCase))
                route = _activeRouteOverride;
            else
                _activeRouteOverride = string.Empty;
        }

        SetNavState(DashboardNavBorder, DashboardAccent, DashboardLabel,
            route.Contains("//dashboard", StringComparison.OrdinalIgnoreCase));
        SetNavState(BillingNavBorder, BillingAccent, BillingLabel,
            route.Contains("//billing", StringComparison.OrdinalIgnoreCase));
        SetNavState(PaymentsNavBorder, PaymentsAccent, PaymentsLabel,
            route.Contains("//payments", StringComparison.OrdinalIgnoreCase));

        var customersActive = route.Contains("//customers", StringComparison.OrdinalIgnoreCase);
        SetNavState(CustomersNavBorder, CustomersAccent, CustomersLabel, customersActive);
        CustomersActionMenu.IsVisible = customersActive && CustomersItem.IsVisible;
        CustomersChevron.Text = customersActive ? "⌄" : "›";

        var customersRegisterActive   = route.Contains("mode=register", StringComparison.OrdinalIgnoreCase);
        var customersManagementActive = customersActive && !customersRegisterActive;
        SetSubItemState(CustomersManagementActionItem, customersManagementActive);
        SetSubItemState(CustomersRegisterActionItem,   customersRegisterActive);

        var reportsActive = route.Contains("//reports", StringComparison.OrdinalIgnoreCase);
        SetNavState(ReportsNavBorder, ReportsAccent, ReportsLabel, reportsActive);
        ReportsActionMenu.IsVisible = reportsActive && ReportsItem.IsVisible;
        ReportsChevron.Text = reportsActive ? "⌄" : "›";

        var reportsIssueActive   = route.Contains("mode=issues", StringComparison.OrdinalIgnoreCase);
        var reportsPrintActive   = route.Contains("mode=print",  StringComparison.OrdinalIgnoreCase);
        var reportsPaymentActive = reportsActive && !reportsIssueActive && !reportsPrintActive;
        SetSubItemState(ReportsPaymentsActionItem, reportsPaymentActive);
        SetSubItemState(ReportsIssueActionItem,    reportsIssueActive);
        SetSubItemState(ReportsPrintActionItem,    reportsPrintActive);

        var settingsActive = route.Contains("//settings",            StringComparison.OrdinalIgnoreCase)
                          || route.Contains(RouteMap.Backup,          StringComparison.OrdinalIgnoreCase)
                          || route.Contains(RouteMap.PrinterSettings, StringComparison.OrdinalIgnoreCase)
                          || route.Contains(RouteMap.PrintQueue,      StringComparison.OrdinalIgnoreCase)
                          || route.Contains(RouteMap.Receipt,         StringComparison.OrdinalIgnoreCase)
                          || route.Contains(RouteMap.Invoice,         StringComparison.OrdinalIgnoreCase);
        SetNavState(SettingsNavBorder, SettingsAccent, SettingsLabel, settingsActive);
        SetNavState(AuditNavBorder, AuditAccent, AuditLabel,
            route.Contains("//audit", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetNavState(Border border, BoxView accent, Label label, bool active)
    {
        border.BackgroundColor = active ? ActiveBg : InactiveBg;
        accent.IsVisible       = active;
        label.FontAttributes   = active ? FontAttributes.Bold : FontAttributes.None;
    }

    private static void SetSubItemState(Border item, bool active)
    {
        item.BackgroundColor = active ? SubActiveBg : SubInactiveBg;
        if (item.Content is Label lbl)
            lbl.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
    }
}
