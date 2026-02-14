using MawasaProject.Presentation.ViewModels.Modules;
using MawasaProject.Presentation.Services.Navigation;

namespace MawasaProject.Presentation.Views.Pages;

public partial class ReportsPage : ContentPage, IQueryAttributable
{
    private ReportsViewModel ViewModel => (ReportsViewModel)BindingContext;

    public ReportsPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ReportsViewModel>();
        ViewModel.InitializeCommand.Execute(null);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("mode", out var value))
        {
            ViewModel.SetModeFromRoute(value?.ToString());
        }
    }

    private async void OnCustomerPaymentReportClicked(object? sender, EventArgs e)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync(RouteMap.ReportsCustomerPaymentHome);
    }

    private async void OnIssueReportClicked(object? sender, EventArgs e)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync(RouteMap.ReportsIssueHome);
    }

    private async void OnPrintReportClicked(object? sender, EventArgs e)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync(RouteMap.ReportsPrintHome);
    }
}
