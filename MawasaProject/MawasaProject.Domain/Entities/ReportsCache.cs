using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class ReportsCache : SoftDeleteEntity
{
    [Required]
    [MaxLength(80)]
    public string CacheKey { get; set; } = string.Empty;

    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}
