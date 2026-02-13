using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class PrinterSettingsPage : ContentPage
{
    private PrinterViewModel ViewModel => (PrinterViewModel)BindingContext;

    public PrinterSettingsPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PrinterViewModel>();
        ViewModel.RefreshCommand.Execute(null);
    }
}
