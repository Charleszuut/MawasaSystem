namespace MawasaProject.Domain.Entities;

public sealed class BackupValidationResult
{
    public string BackupFilePath { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public bool HashCheckAvailable { get; init; }
    public bool HashMatches { get; init; }
    public bool SqliteIntegrityOk { get; init; }
    public string? ExpectedHash { get; init; }
    public string? ActualHash { get; init; }
    public string Message { get; init; } = string.Empty;
}
