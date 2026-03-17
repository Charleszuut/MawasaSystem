using MawasaProject.Presentation.ViewModels.Modules;
using MawasaProject.Presentation.Diagnostics;

namespace MawasaProject.Presentation.Views.Pages;

public partial class PaymentsPage : ContentPage
{
    private const double FocusedStrokeThickness = 2;
    private const double DefaultStrokeThickness = 1;

    public PaymentsPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<PaymentsViewModel>();
    }

    private void OnEntryFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || entry.Parent is not Border border)
        {
            return;
        }

        border.Stroke = ResolveColor("PrimaryBlue", Colors.DodgerBlue);
        border.StrokeThickness = FocusedStrokeThickness;
        entry.BackgroundColor = Colors.Transparent;
    }

    private void OnEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || entry.Parent is not Border border)
        {
            return;
        }

        border.Stroke = ResolveColor("BorderStrong", Colors.LightGray);
        border.StrokeThickness = DefaultStrokeThickness;
        entry.BackgroundColor = Colors.Transparent;
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (BindingContext is not PaymentsViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.LiveSearchAsync(e.NewTextValue);
        }
        catch (Exception exception)
        {
            AppDiagnostics.LogException("PaymentsPage.LiveSearchAsync", exception);
        }
    }

    private Color ResolveColor(string key, Color fallback)
    {
        if (Resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }

        return fallback;
    }
}
