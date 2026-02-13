using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class PrintQueuePage : ContentPage
{
    private PrintQueueViewModel ViewModel => (PrintQueueViewModel)BindingContext;

    public PrintQueuePage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PrintQueueViewModel>();
        ViewModel.RefreshCommand.Execute(null);
    }
}
