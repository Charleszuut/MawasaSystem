namespace MawasaProject.Presentation.Services.Navigation;

public sealed class NavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        route = NormalizeShellRoute(route);
        if (parameters is null || parameters.Count == 0)
        {
            return Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
        }

        return Microsoft.Maui.Controls.Shell.Current.GoToAsync(route, parameters);
    }

    public Task GoBackAsync() => Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");

    private static string NormalizeShellRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return route;
        }

        if (!route.StartsWith("//", StringComparison.Ordinal))
        {
            return route;
        }

        var trimmed = route.TrimStart('/');
        var queryIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;
        var query = queryIndex >= 0 ? trimmed[queryIndex..] : string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return route;
        }

        var shell = Microsoft.Maui.Controls.Shell.Current;
        if (shell is null)
        {
            return route;
        }

        var targetItem = shell.Items.FirstOrDefault(item =>
            string.Equals(item.Route, segments[0], StringComparison.OrdinalIgnoreCase));
        if (targetItem is null)
        {
            return route;
        }

        if (segments.Length > 1)
        {
            return $"//{targetItem.Route}/{string.Join('/', segments.Skip(1))}{query}";
        }

        var section = targetItem.Items.FirstOrDefault();
        if (section is null)
        {
            return $"//{targetItem.Route}{query}";
        }

        var content = section.Items.FirstOrDefault();
        if (content is null)
        {
            return $"//{targetItem.Route}/{section.Route}{query}";
        }

        if (!string.IsNullOrWhiteSpace(section.Route)
            && !string.IsNullOrWhiteSpace(content.Route)
            && !string.Equals(section.Route, content.Route, StringComparison.Ordinal))
        {
            return $"//{targetItem.Route}/{section.Route}/{content.Route}{query}";
        }

        if (!string.IsNullOrWhiteSpace(content.Route))
        {
            return $"//{targetItem.Route}/{content.Route}{query}";
        }

        return $"//{targetItem.Route}{query}";
    }
}
