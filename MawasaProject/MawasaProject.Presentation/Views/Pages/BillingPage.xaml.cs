using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class BillingPage : ContentPage
{
    private BillingViewModel ViewModel => (BillingViewModel)BindingContext;

    private bool _hasInitialized;

    public BillingPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<BillingViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasInitialized)
        {
            return;
        }

        _hasInitialized = true;

        try
        {
            await ViewModel.RefreshLedgerCommand.ExecuteAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Billing", exception.Message, "OK");
        }
    }
}
