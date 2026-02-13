using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.Entities;

public sealed class Payment : AuditableEntity
{
    [Required]
    public Guid BillId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime PaymentDateUtc { get; set; } = DateTime.UtcNow;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    public Guid CreatedByUserId { get; set; }

    public void MarkCompleted(string referenceNumber, DateTime? paymentDateUtc = null)
    {
        DomainGuard.AgainstNullOrWhiteSpace(referenceNumber, nameof(referenceNumber));
        Status = PaymentStatus.Completed;
        ReferenceNumber = referenceNumber.Trim();
        PaymentDateUtc = paymentDateUtc ?? DateTime.UtcNow;
        Touch(PaymentDateUtc);
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
        Touch();
    }
}
