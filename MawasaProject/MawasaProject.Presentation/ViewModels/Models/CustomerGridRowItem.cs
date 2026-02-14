namespace MawasaProject.Presentation.ViewModels.Models;

public sealed class CustomerGridRowItem
{
    public Guid CustomerId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string ApplicationText { get; init; } = string.Empty;
    public string ApplicationBackground { get; init; } = "#EEF3FB";
    public string ApplicationForeground { get; init; } = "#4B5E75";
    public string ContactNumber { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string StatusBackground { get; init; } = "#E8F8EE";
    public string StatusForeground { get; init; } = "#1F8A57";
    public DateTime CreatedAtUtc { get; init; }
    public string CreatedDisplay { get; init; } = string.Empty;
    public string RowBackground { get; init; } = "#F9FBFE";
}
