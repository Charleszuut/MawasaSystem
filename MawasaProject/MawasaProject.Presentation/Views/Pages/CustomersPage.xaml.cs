using MawasaProject.Presentation.ViewModels.Modules;
using MawasaProject.Presentation.Services.Navigation;

namespace MawasaProject.Presentation.Views.Pages;

public partial class CustomersPage : ContentPage, IQueryAttributable
{
    private CustomersViewModel ViewModel => (CustomersViewModel)BindingContext;

    public CustomersPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<CustomersViewModel>();
        ViewModel.SearchCommand.Execute(null);
        ViewModel.SetModeFromRoute(ResolveModeFromCurrentRoute());
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("mode", out var mode))
        {
            ViewModel.SetModeFromRoute(mode?.ToString());
            return;
        }

        ViewModel.SetModeFromRoute(ResolveModeFromCurrentRoute());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.SetModeFromRoute(ResolveModeFromCurrentRoute());
    }

    private async void OnRegisterBackClicked(object? sender, EventArgs e)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync(RouteMap.CustomersManagementHome);
    }

    private static string? ResolveModeFromCurrentRoute()
    {
        var route = Microsoft.Maui.Controls.Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;
        return route.Contains("mode=register", StringComparison.OrdinalIgnoreCase) ? "register" : "manage";
    }
}
