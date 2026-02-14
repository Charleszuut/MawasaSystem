using System.Collections.ObjectModel;
using MawasaProject.Presentation.Validation;

namespace MawasaProject.Presentation.ViewModels.Core;

public abstract class BaseViewModel : ObservableObject
{
    private bool _isBusy;
    private string _title = string.Empty;
    private string? _errorMessage;
    private string? _statusMessage;
    private ViewModelState _state = ViewModelState.Idle;
    private CancellationTokenSource? _busyCts;

    protected BaseViewModel()
    {
        ValidationErrors = [];
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasErrors));
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ViewModelState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public bool HasErrors => !string.IsNullOrWhiteSpace(ErrorMessage) || ValidationErrors.Count > 0;

    public ObservableCollection<ValidationError> ValidationErrors { get; }

    protected void SetValidationErrors(IEnumerable<ValidationError> errors)
    {
        ValidationErrors.Clear();
        foreach (var error in errors)
        {
            ValidationErrors.Add(error);
        }

        RaisePropertyChanged(nameof(HasErrors));
    }

    protected void ClearValidationErrors()
    {
        if (ValidationErrors.Count == 0)
        {
            return;
        }

        ValidationErrors.Clear();
        RaisePropertyChanged(nameof(HasErrors));
    }

    protected async Task RunBusyAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        _busyCts?.Dispose();
        _busyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            IsBusy = true;
            State = ViewModelState.Loading;
            ErrorMessage = null;
            ClearValidationErrors();
            await action(_busyCts.Token);
            State = ViewModelState.Ready;
        }
        catch (OperationCanceledException)
        {
            State = ViewModelState.Idle;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            State = ViewModelState.Error;
        }
        finally
        {
            IsBusy = false;
            _busyCts?.Dispose();
            _busyCts = null;
        }
    }

    protected async Task RunBusyAsync(Func<Task> action)
    {
        await RunBusyAsync(_ => action());
    }

    protected void CancelCurrentOperation()
    {
        _busyCts?.Cancel();
    }
}
