using System.Text;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class DocumentArtifactWriter(PdfGenerator pdfGenerator)
{
    public async Task<DocumentArtifacts> WriteAsync(
        string outputDirectory,
        string documentNumber,
        string renderedContent,
        IReadOnlyDictionary<string, string?> metadata,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var pdfPath = await pdfGenerator.SaveAsPdfAsync(outputDirectory, documentNumber + ".pdf", renderedContent, cancellationToken);

        var layoutPath = Path.Combine(outputDirectory, documentNumber + ".txt");
        await File.WriteAllTextAsync(layoutPath, renderedContent, cancellationToken);

        var imagePath = Path.Combine(outputDirectory, documentNumber + ".svg");
        await File.WriteAllTextAsync(imagePath, BuildSvg(renderedContent), cancellationToken);

        var csvPath = Path.Combine(outputDirectory, documentNumber + "_reference.csv");
        await File.WriteAllTextAsync(csvPath, BuildMetadataCsv(metadata), cancellationToken);

        return new DocumentArtifacts
        {
            PdfPath = pdfPath,
            LayoutPath = layoutPath,
            ImagePath = imagePath,
            CsvReferencePath = csvPath
        };
    }

    private static string BuildMetadataCsv(IReadOnlyDictionary<string, string?> metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Key,Value");

        foreach (var item in metadata)
        {
            builder
                .Append(EscapeCsv(item.Key))
                .Append(',')
                .Append(EscapeCsv(item.Value ?? string.Empty))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        if (escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
        {
            return "\"" + escaped + "\"";
        }

        return escaped;
    }

    private static string BuildSvg(string renderedContent)
    {
        var lines = renderedContent.Split('\n');
        var escapedLines = lines
            .Select(static l => System.Security.SecurityElement.Escape(l) ?? string.Empty)
            .ToArray();

        var height = 30 + (escapedLines.Length * 20);
        var textElements = new StringBuilder();
        for (var i = 0; i < escapedLines.Length; i++)
        {
            textElements.AppendLine($"<text x=\"12\" y=\"{24 + (i * 20)}\" font-family=\"Consolas\" font-size=\"14\">{escapedLines[i]}</text>");
        }

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="900" height="{height}">
              <rect x="0" y="0" width="100%" height="100%" fill="#ffffff" stroke="#d1d5db" />
              {textElements}
            </svg>
            """;
    }

    public sealed class DocumentArtifacts
    {
        public string PdfPath { get; init; } = string.Empty;
        public string LayoutPath { get; init; } = string.Empty;
        public string ImagePath { get; init; } = string.Empty;
        public string CsvReferencePath { get; init; } = string.Empty;
    }
}
