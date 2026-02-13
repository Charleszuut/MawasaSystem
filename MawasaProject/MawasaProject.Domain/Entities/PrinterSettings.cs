namespace MawasaProject.Domain.Entities;

public sealed class PrinterSettings
{
    public string DefaultPrinter { get; set; } = string.Empty;
    public string PaperSize { get; set; } = "A4";
    public bool EnableRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}
