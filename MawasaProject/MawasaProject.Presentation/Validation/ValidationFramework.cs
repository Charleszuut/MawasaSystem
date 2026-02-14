using System.ComponentModel.DataAnnotations;

namespace MawasaProject.Presentation.Validation;

public sealed class ValidationError
{
    public required string PropertyName { get; init; }
    public required string Message { get; init; }
}

public static class ValidationFramework
{
    public static IReadOnlyList<ValidationError> ValidateRequired(string propertyName, string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<ValidationError>();
        }

        return [new ValidationError { PropertyName = propertyName, Message = message }];
    }

    public static IReadOnlyList<ValidationError> ValidateMinLength(string propertyName, string? value, int minLength, string message)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length >= minLength)
        {
            return Array.Empty<ValidationError>();
        }

        return [new ValidationError { PropertyName = propertyName, Message = message }];
    }

    public static IReadOnlyList<ValidationError> ValidateEmail(string propertyName, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<ValidationError>();
        }

        var validator = new EmailAddressAttribute();
        return validator.IsValid(value)
            ? Array.Empty<ValidationError>()
            : [new ValidationError { PropertyName = propertyName, Message = message }];
    }

    public static IReadOnlyList<ValidationError> ValidatePositiveAmount(string propertyName, decimal value, string message)
    {
        return value > 0m
            ? Array.Empty<ValidationError>()
            : [new ValidationError { PropertyName = propertyName, Message = message }];
    }

    public static IReadOnlyList<ValidationError> ValidateObject(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        if (results.Count == 0)
        {
            return Array.Empty<ValidationError>();
        }

        return results
            .SelectMany(result =>
                result.MemberNames.DefaultIfEmpty(string.Empty).Select(member => new ValidationError
                {
                    PropertyName = member,
                    Message = result.ErrorMessage ?? "Validation failed."
                }))
            .ToArray();
    }

    public static IReadOnlyList<ValidationError> Combine(params IReadOnlyList<ValidationError>[] validations)
    {
        if (validations.Length == 0)
        {
            return Array.Empty<ValidationError>();
        }

        return validations.SelectMany(static x => x).ToArray();
    }
}
