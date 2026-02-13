using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class AuditPage : ContentPage
{
    private AuditViewModel ViewModel => (AuditViewModel)BindingContext;

    public AuditPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<AuditViewModel>();
        ViewModel.RefreshCommand.Execute(null);
    }
}
