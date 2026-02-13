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
}
