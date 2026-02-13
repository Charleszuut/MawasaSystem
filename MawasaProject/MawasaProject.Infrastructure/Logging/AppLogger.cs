using Microsoft.Extensions.Logging;
using MawasaProject.Application.Abstractions.Logging;

namespace MawasaProject.Infrastructure.Logging;

public sealed class AppLogger<T>(ILogger<T> logger) : IAppLogger<T>
{
    public void Info(string message, params object[] args) => logger.LogInformation(message, args);

    public void Warn(string message, params object[] args) => logger.LogWarning(message, args);

    public void Error(Exception exception, string message, params object[] args) => logger.LogError(exception, message, args);
}
