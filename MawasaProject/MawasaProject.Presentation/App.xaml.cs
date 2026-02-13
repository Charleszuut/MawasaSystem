using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Infrastructure.Services.Backup;

namespace MawasaProject.Presentation;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _serviceProvider;

    public static IServiceProvider Services { get; private set; } = default!;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider;
        Services = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var loadingPage = CreateLoadingPage("Starting Mawasa Project...");
        var window = new Window(loadingPage);

        _ = InitializeAsync(window);
        return window;
    }

    private async Task InitializeAsync(Window window)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            using var startupCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await initializer.InitializeAsync(startupCts.Token);

            var scheduler = _serviceProvider.GetRequiredService<BackupScheduler>();
            scheduler.Start(TimeSpan.FromHours(12));

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                window.Page = new Shell.AppShell();
            });
        }
        catch (Exception exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                window.Page = CreateLoadingPage(
                    "Startup failed",
                    "Initialization did not complete.",
                    exception.Message);
            });
        }
    }

    private static ContentPage CreateLoadingPage(string title, string? subtitle = null, string? details = null)
    {
        return new ContentPage
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        WidthRequest = 48,
                        HeightRequest = 48
                    },
                    new Label
                    {
                        Text = title,
                        FontSize = 22,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = subtitle ?? "Please wait while local services initialize.",
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = details ?? string.Empty,
                        IsVisible = !string.IsNullOrWhiteSpace(details),
                        TextColor = Colors.IndianRed,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }
}
