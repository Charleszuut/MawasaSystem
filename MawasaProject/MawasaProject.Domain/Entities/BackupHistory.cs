using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class BackupHistory : AuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Version { get; set; } = "v1";
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsAutomatic { get; set; }
    public bool IsEncrypted { get; set; }
    public DateTime? IntegrityVerifiedAtUtc { get; set; }
    public string? Notes { get; set; }
}
