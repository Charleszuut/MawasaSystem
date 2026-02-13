namespace MawasaProject.Domain.ValueObjects;

public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Create(decimal amount, string currency = "USD")
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length is < 3 or > 5)
        {
            throw new ArgumentException("Currency must be 3-5 characters.", nameof(currency));
        }

        return new Money(decimal.Round(amount, 2), currency.ToUpperInvariant());
    }
}
