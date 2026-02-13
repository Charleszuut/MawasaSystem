using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class InvoicePage : ContentPage
{
    public InvoicePage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<InvoiceViewModel>();
    }
}
