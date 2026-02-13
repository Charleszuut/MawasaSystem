namespace MawasaProject.Domain.Common;

public static class DomainGuard
{
    public static void AgainstEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException($"{paramName} cannot be empty.");
        }
    }

    public static void AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{paramName} is required.");
        }
    }

    public static void AgainstNegative(decimal value, string paramName)
    {
        if (value < 0)
        {
            throw new DomainValidationException($"{paramName} cannot be negative.");
        }
    }

    public static void AgainstOutOfRange(decimal value, decimal min, decimal max, string paramName)
    {
        if (value < min || value > max)
        {
            throw new DomainValidationException($"{paramName} must be between {min} and {max}.");
        }
    }
}
