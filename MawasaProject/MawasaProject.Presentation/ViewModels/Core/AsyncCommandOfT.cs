using System.Windows.Input;

namespace MawasaProject.Presentation.ViewModels.Core;

public sealed class AsyncCommandOfT<T>(
    Func<T, Task> executeAsync,
    Func<T, bool>? canExecute = null,
    Action<Exception>? onException = null) : IAsyncCommand
{
    private bool _isExecuting;

    public bool IsExecuting => _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (parameter is not T typedParam && parameter != null)
        {
            return false;
        }

        return !_isExecuting && (canExecute?.Invoke((T)parameter!) ?? true);
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await executeAsync((T)parameter!);
        }
        catch (Exception exception)
        {
            onException?.Invoke(exception);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
