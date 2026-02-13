namespace MawasaProject.Application.Abstractions.Logging;

public interface IAppLogger<T>
{
    void Info(string message, params object[] args);
    void Warn(string message, params object[] args);
    void Error(Exception exception, string message, params object[] args);
}
