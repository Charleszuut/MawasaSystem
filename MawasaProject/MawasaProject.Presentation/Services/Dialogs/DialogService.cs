namespace MawasaProject.Presentation.Services.Dialogs;

public sealed class DialogService : IDialogService
{
    public Task AlertAsync(string title, string message, string cancel = "OK")
    {
        return Microsoft.Maui.Controls.Shell.Current.DisplayAlertAsync(title, message, cancel);
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        return Microsoft.Maui.Controls.Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
    }
}
