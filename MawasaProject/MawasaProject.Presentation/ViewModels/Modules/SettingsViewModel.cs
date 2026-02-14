using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class SettingsViewModel(
    IAuthService authService,
    AppStateStore appStateStore,
    INavigationService navigationService,
    IDialogService dialogService) : BaseViewModel
{
    public SettingsViewModel() : this(
        App.Services.GetRequiredService<IAuthService>(),
        App.Services.GetRequiredService<AppStateStore>(),
        App.Services.GetRequiredService<INavigationService>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

    public AsyncCommand OpenBackupCommand => new(() => navigationService.GoToAsync(RouteMap.Backup));
    public AsyncCommand OpenPrinterSettingsCommand => new(() => navigationService.GoToAsync(RouteMap.PrinterSettings));
    public AsyncCommand OpenPrintQueueCommand => new(() => navigationService.GoToAsync(RouteMap.PrintQueue));
    public AsyncCommand LogoutCommand => new(async () => await RunBusyAsync(async () =>
    {
        var shouldLogout = await dialogService.ConfirmAsync("Logout", "Sign out from this device?");
        if (!shouldLogout)
        {
            return;
        }

        await authService.LogoutAsync();
        appStateStore.Session = null;
        await navigationService.GoToAsync(RouteMap.LoginRoot);
    }));
}
