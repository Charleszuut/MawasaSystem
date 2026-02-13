namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class LayoutRenderer
{
    public string Render(string title, string body, string? branding = null)
    {
        var header = string.IsNullOrWhiteSpace(branding) ? title : $"{branding}\n{title}";
        return $"====================\n{header}\n====================\n{body}\n";
    }
}
