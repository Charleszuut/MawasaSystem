using System.Windows.Input;

namespace MawasaProject.Presentation.ViewModels.Core;

public sealed class RelayCommandOfT<T>(
    Action<T?> execute,
    Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke(Coerce(parameter)) ?? true;
    }

    public void Execute(object? parameter)
    {
        execute(Coerce(parameter));
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Coerce(object? parameter)
    {
        if (parameter is null)
        {
            return default;
        }

        return parameter is T typed ? typed : default;
    }
}
