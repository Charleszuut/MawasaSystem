using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class SettingsViewModel(INavigationService navigationService) : BaseViewModel
{
    public SettingsViewModel() : this(App.Services.GetRequiredService<INavigationService>())
    {
    }

    public AsyncCommand OpenBackupCommand => new(() => navigationService.GoToAsync("backup"));
    public AsyncCommand OpenPrinterSettingsCommand => new(() => navigationService.GoToAsync("printer-settings"));
    public AsyncCommand OpenPrintQueueCommand => new(() => navigationService.GoToAsync("print-queue"));
}
