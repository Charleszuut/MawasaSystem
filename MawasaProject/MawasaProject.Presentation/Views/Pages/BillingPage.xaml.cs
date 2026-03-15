using MawasaProject.Presentation.ViewModels.Modules;
using MawasaProject.Presentation.Diagnostics;

namespace MawasaProject.Presentation.Views.Pages;

public partial class BillingPage : ContentPage
{
    private BillingViewModel ViewModel => (BillingViewModel)BindingContext;

    private bool _hasInitialized;

    public BillingPage()
    {
        try
        {
            InitializeComponent();
            BindingContext = App.Services.GetRequiredService<BillingViewModel>();
        }
        catch (Exception exception)
        {
            AppDiagnostics.LogException("BillingPage.InitializeComponent", exception);
            Content = BuildFallbackContent(exception);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is not BillingViewModel)
        {
            return;
        }

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
            AppDiagnostics.LogException("BillingPage.OnAppearing", exception);
            await DisplayAlertAsync("Billing", exception.Message, "OK");
        }
    }

    private static View BuildFallbackContent(Exception exception)
    {
        return new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Billing page failed to load.",
                    FontSize = 18,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = exception.Message,
                    TextColor = Colors.IndianRed,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
    }

}
