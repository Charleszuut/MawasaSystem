namespace MawasaProject.Presentation.Services.Navigation;

public sealed class NavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
        }

        return Microsoft.Maui.Controls.Shell.Current.GoToAsync(route, parameters);
    }

    public Task GoBackAsync() => Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
}
