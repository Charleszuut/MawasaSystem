using System.ComponentModel.DataAnnotations;

namespace MawasaProject.Application.Validation;

public static class EntityValidator
{
    public static void ValidateObject<T>(T value)
    {
        var context = new ValidationContext(value!);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(value!, context, results, validateAllProperties: true))
        {
            var message = string.Join("; ", results.Select(static x => x.ErrorMessage));
            throw new ValidationException(message);
        }
    }
}
