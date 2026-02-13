using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class LoginPage : ContentPage
{
    private LoginViewModel ViewModel => (LoginViewModel)BindingContext;

    public LoginPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<LoginViewModel>();
    }
}
