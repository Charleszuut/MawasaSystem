namespace MawasaProject.Domain.DTOs;

public sealed class DocumentGenerationResult
{
    public string DocumentNumber { get; init; } = string.Empty;
    public string PdfPath { get; init; } = string.Empty;
    public string LayoutPath { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
    public string CsvReferencePath { get; init; } = string.Empty;
    public Guid? PrintJobId { get; init; }
}
