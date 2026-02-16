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
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordHidden = true;
    private bool _rememberMe = true;

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

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set
        {
            if (SetProperty(ref _isPasswordHidden, value))
            {
                RaisePropertyChanged(nameof(PasswordToggleText));
            }
        }
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";

    public RelayCommand TogglePasswordVisibilityCommand => new(() =>
    {
        IsPasswordHidden = !IsPasswordHidden;
    });

    public AsyncCommand ForgotPasswordCommand => new(async () =>
    {
        await dialogService.AlertAsync("Password recovery", "Offline mode is enabled. Please contact an Admin to reset your password.");
    });

    public AsyncCommand LoginCommand => new(async () => await RunBusyAsync(async () =>
    {
        var errors = ValidationFramework.Combine(
            ValidationFramework.ValidateRequired(nameof(Username), Username, "Username is required."),
            ValidationFramework.ValidateRequired(nameof(Password), Password, "Password is required."),
            ValidationFramework.ValidateMinLength(nameof(Password), Password, 8, "Password must be at least 8 characters."));
        SetValidationErrors(errors);

        if (errors.Count > 0)
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
        StatusMessage = "Login successful.";
        await navigationService.GoToAsync(RouteMap.DashboardHome);
    }));
}
