using System.Threading.Tasks;
using MawasaProject.Presentation.ViewModels.Modules;

namespace MawasaProject.Presentation.Views.Pages;

public partial class LoginPage : ContentPage
{
    private LoginViewModel ViewModel => (LoginViewModel)BindingContext;

    private bool _hasPlayedEntranceAnimation;

    public LoginPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<LoginViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasPlayedEntranceAnimation)
        {
            return;
        }

        _hasPlayedEntranceAnimation = true;

        if (LoginCard is null)
        {
            return;
        }

        LoginCard.AbortAnimation("LoginEnter");

        var cardOriginalOpacity = LoginCard.Opacity;
        var cardOriginalTranslationY = LoginCard.TranslationY;
        var cardOriginalScale = LoginCard.Scale;

        LoginCard.Opacity = 0;
        LoginCard.TranslationY = cardOriginalTranslationY + 26;
        LoginCard.Scale = cardOriginalScale * 0.96;

        if (LogoContainer is not null)
        {
            LogoContainer.Opacity = 0;
            LogoContainer.Scale = 0.85;
        }

        await Task.WhenAll(
            LoginCard.FadeToAsync(cardOriginalOpacity, 220U, Easing.CubicOut),
            LoginCard.TranslateToAsync(LoginCard.TranslationX, cardOriginalTranslationY, 260U, Easing.CubicOut),
            LoginCard.ScaleToAsync(cardOriginalScale, 260U, Easing.CubicOut),
            LogoContainer is null
                ? Task.CompletedTask
                : Task.WhenAll(
                    LogoContainer.FadeToAsync(1, 240U, Easing.CubicOut),
                    LogoContainer.ScaleToAsync(1, 340U, Easing.SpringOut)));
    }
}
