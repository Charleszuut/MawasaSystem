using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class BackupPage : ContentPage
{
    private BackupViewModel ViewModel => (BackupViewModel)BindingContext;

    public BackupPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<BackupViewModel>();
        ViewModel.RefreshCommand.Execute(null);
    }
}
