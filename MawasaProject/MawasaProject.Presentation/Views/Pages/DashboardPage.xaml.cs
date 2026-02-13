using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class DashboardPage : ContentPage
{
    private DashboardViewModel ViewModel => (DashboardViewModel)BindingContext;

    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<DashboardViewModel>();
        ViewModel.RefreshCommand.Execute(null);
    }
}
