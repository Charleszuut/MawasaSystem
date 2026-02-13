using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;

namespace MawasaProject.Domain.Entities;

public sealed class Customer : SoftDeleteEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [EmailAddress]
    [MaxLength(120)]
    public string? Email { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    public ICollection<Bill> Bills { get; init; } = new List<Bill>();

    public void UpdateContact(string? phoneNumber, string? email, string? address)
    {
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Touch();
    }
}
