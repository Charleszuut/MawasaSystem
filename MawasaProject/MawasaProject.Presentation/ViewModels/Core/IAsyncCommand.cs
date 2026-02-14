using System.Windows.Input;

namespace MawasaProject.Presentation.ViewModels.Core;

public interface IAsyncCommand : ICommand
{
    bool IsExecuting { get; }
    Task ExecuteAsync(object? parameter = null);
}
