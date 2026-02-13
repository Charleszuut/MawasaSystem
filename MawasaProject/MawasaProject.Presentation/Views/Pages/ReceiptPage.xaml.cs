using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class ReceiptPage : ContentPage
{
    public ReceiptPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<ReceiptViewModel>();
    }
}
