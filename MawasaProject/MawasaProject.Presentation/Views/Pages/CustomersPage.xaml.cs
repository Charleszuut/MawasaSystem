using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class CustomersPage : ContentPage
{
    private CustomersViewModel ViewModel => (CustomersViewModel)BindingContext;

    public CustomersPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<CustomersViewModel>();
        ViewModel.SearchCommand.Execute(null);
    }
}
