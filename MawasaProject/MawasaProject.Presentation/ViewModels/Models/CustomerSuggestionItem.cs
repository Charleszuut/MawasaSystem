using MawasaProject.Presentation.ViewModels.Core;
using System.Windows.Input;

namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class CustomerSuggestionItem : ObservableObject
{
    private bool _isHighlighted;

    public Guid CustomerId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public ICommand? SelectCommand { get; init; }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }
}
