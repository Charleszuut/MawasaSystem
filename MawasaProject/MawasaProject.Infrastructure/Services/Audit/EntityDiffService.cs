using System.Text.Json;

namespace MawasaProject.Infrastructure.Services.Audit;

public sealed class EntityDiffService
{
    public (string? OldValues, string? NewValues) Diff(object? oldValue, object? newValue)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        var oldJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, options);
        var newJson = newValue is null ? null : JsonSerializer.Serialize(newValue, options);
        return (oldJson, newJson);
    }
}
