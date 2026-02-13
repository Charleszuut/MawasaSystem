using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class BillingPage : ContentPage
{
    public BillingPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<BillingViewModel>();
    }
}
