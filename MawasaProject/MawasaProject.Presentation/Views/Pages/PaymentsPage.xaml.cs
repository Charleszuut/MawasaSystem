using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class PaymentsPage : ContentPage
{
    public PaymentsPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PaymentsViewModel>();
    }
}
