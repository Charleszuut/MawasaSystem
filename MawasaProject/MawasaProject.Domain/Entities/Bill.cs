using System.ComponentModel.DataAnnotations;
using MawasaProject.Domain.Common;
using MawasaProject.Domain.Enums;

namespace MawasaProject.Domain.Entities;

public sealed class Bill : SoftDeleteEntity
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [MaxLength(40)]
    public string BillNumber { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Balance { get; set; }

    public DateTime DueDateUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Pending;

    public Guid CreatedByUserId { get; set; }

    /// <summary>True once the bill has been physically printed and dispatched to the customer.</summary>
    public bool IsPrinted { get; set; }

    /// <summary>UTC timestamp when the bill was first printed.</summary>
    public DateTime? PrintedAtUtc { get; set; }

    public ICollection<Payment> Payments { get; init; } = new List<Payment>();

    public ICollection<BillStatusHistory> StatusHistory { get; init; } = new List<BillStatusHistory>();

    /// <summary>Marks this bill as printed. Idempotent – calling twice keeps the original timestamp.</summary>
    public void MarkPrinted(DateTime? printedAtUtc = null)
    {
        if (IsPrinted)
        {
            return;
        }

        IsPrinted = true;
        PrintedAtUtc = printedAtUtc ?? DateTime.UtcNow;
        Touch(PrintedAtUtc);
    }

    public void InitializeForCreate()
    {
        DomainGuard.AgainstEmpty(CustomerId, nameof(CustomerId));
        DomainGuard.AgainstNullOrWhiteSpace(BillNumber, nameof(BillNumber));
        DomainGuard.AgainstNegative(Amount, nameof(Amount));

        Balance = Amount;
        Status = BillStatus.Pending;
        PaidAtUtc = null;
        Touch();
    }

    public void ApplyPayment(decimal paymentAmount, DateTime? paidAtUtc = null)
    {
        DomainGuard.AgainstOutOfRange(paymentAmount, 0.01m, decimal.MaxValue, nameof(paymentAmount));
        DomainGuard.AgainstNegative(Balance, nameof(Balance));

        Balance = Math.Max(0m, Balance - paymentAmount);

        if (Balance == 0m)
        {
            Status = BillStatus.Paid;
            PaidAtUtc = paidAtUtc ?? DateTime.UtcNow;
            Touch(PaidAtUtc);
            return;
        }

        if (DueDateUtc < DateTime.UtcNow)
        {
            Status = BillStatus.Overdue;
        }
        else
        {
            Status = BillStatus.Pending;
        }

        Touch();
    }

    public void RecalculateFromTotalPaid(decimal totalPaid, DateTime asOfUtc)
    {
        DomainGuard.AgainstNegative(totalPaid, nameof(totalPaid));

        Balance = Math.Max(0m, Amount - totalPaid);
        if (Balance == 0m)
        {
            Status = BillStatus.Paid;
            PaidAtUtc = asOfUtc;
            Touch(asOfUtc);
            return;
        }

        PaidAtUtc = null;
        Status = DueDateUtc < asOfUtc ? BillStatus.Overdue : BillStatus.Pending;
        Touch(asOfUtc);
    }

    public void MarkPaid(DateTime? paidAtUtc = null)
    {
        Balance = 0m;
        Status = BillStatus.Paid;
        PaidAtUtc = paidAtUtc ?? DateTime.UtcNow;
        Touch(PaidAtUtc);
    }

    public void MarkPending(DateTime? changedAtUtc = null)
    {
        if (Status == BillStatus.Paid && Balance == 0m)
        {
            return;
        }

        Status = BillStatus.Pending;
        if (Balance == 0m)
        {
            Balance = Amount;
        }

        PaidAtUtc = null;
        Touch(changedAtUtc);
    }

    public void MarkOverdue(DateTime? changedAtUtc = null)
    {
        if (Status == BillStatus.Paid)
        {
            return;
        }

        Status = BillStatus.Overdue;
        Touch(changedAtUtc);
    }
}
