using System.Windows.Input;

namespace MawasaProject.Presentation.Behaviors;

public sealed class FocusCommandBehavior : Behavior<VisualElement>
{
    public static readonly BindableProperty FocusedCommandProperty =
        BindableProperty.Create(nameof(FocusedCommand), typeof(ICommand), typeof(FocusCommandBehavior));

    public static readonly BindableProperty UnfocusedCommandProperty =
        BindableProperty.Create(nameof(UnfocusedCommand), typeof(ICommand), typeof(FocusCommandBehavior));

    public ICommand? FocusedCommand
    {
        get => (ICommand?)GetValue(FocusedCommandProperty);
        set => SetValue(FocusedCommandProperty, value);
    }

    public ICommand? UnfocusedCommand
    {
        get => (ICommand?)GetValue(UnfocusedCommandProperty);
        set => SetValue(UnfocusedCommandProperty, value);
    }

    protected override void OnAttachedTo(VisualElement bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.Focused += OnFocused;
        bindable.Unfocused += OnUnfocused;
    }

    protected override void OnDetachingFrom(VisualElement bindable)
    {
        bindable.Focused -= OnFocused;
        bindable.Unfocused -= OnUnfocused;
        base.OnDetachingFrom(bindable);
    }

    private void OnFocused(object? sender, FocusEventArgs e)
    {
        ExecuteCommand(FocusedCommand);
    }

    private void OnUnfocused(object? sender, FocusEventArgs e)
    {
        ExecuteCommand(UnfocusedCommand);
    }

    private static void ExecuteCommand(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
