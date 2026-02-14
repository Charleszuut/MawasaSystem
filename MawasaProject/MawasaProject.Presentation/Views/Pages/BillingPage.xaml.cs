using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class BillingPage : ContentPage
{
    private BillingViewModel ViewModel => (BillingViewModel)BindingContext;

    public BillingPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<BillingViewModel>();
        ViewModel.RefreshLedgerCommand.Execute(null);
    }
}
