using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.Services.Navigation;
using MawasaProject.Presentation.Validation;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class LoginViewModel(
    IAuthService authService,
    ISessionService sessionService,
    AppStateStore appStateStore,
    INavigationService navigationService,
    IDialogService dialogService) : BaseViewModel
{
    private string _username = "admin";
    private string _password = "Admin@123";

    public LoginViewModel() : this(
        App.Services.GetRequiredService<IAuthService>(),
        App.Services.GetRequiredService<ISessionService>(),
        App.Services.GetRequiredService<AppStateStore>(),
        App.Services.GetRequiredService<INavigationService>(),
        App.Services.GetRequiredService<IDialogService>())
    {
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public AsyncCommand LoginCommand => new(async () => await RunBusyAsync(async () =>
    {
        var usernameErrors = ValidationFramework.ValidateRequired(nameof(Username), Username, "Username is required.");
        var passwordErrors = ValidationFramework.ValidateRequired(nameof(Password), Password, "Password is required.");
        var errors = usernameErrors.Concat(passwordErrors).ToArray();
        if (errors.Length > 0)
        {
            await dialogService.AlertAsync("Validation", string.Join("\n", errors.Select(static x => x.Message)));
            return;
        }

        var result = await authService.LoginAsync(Username, Password);
        if (!result.Success)
        {
            await dialogService.AlertAsync("Login failed", result.Message);
            return;
        }

        appStateStore.Session = sessionService.CurrentSession;
        await navigationService.GoToAsync("//dashboard/home");
    }));
}
