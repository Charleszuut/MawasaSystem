namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class LayoutRenderer
{
    public string Render(string title, string body, string? branding = null)
    {
        var header = string.IsNullOrWhiteSpace(branding) ? title : $"{branding}\n{title}";
        var divider = new string('=', 44);
        return $"{divider}\n{header}\n{divider}\n{body}\n{divider}\nRenderedAtUtc: {DateTime.UtcNow:O}\n";
    }
}
