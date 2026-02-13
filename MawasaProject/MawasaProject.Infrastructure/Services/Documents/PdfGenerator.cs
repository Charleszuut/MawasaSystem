namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class PdfGenerator
{
    public async Task<string> SaveAsPdfAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content, cancellationToken);
        return path;
    }
}
