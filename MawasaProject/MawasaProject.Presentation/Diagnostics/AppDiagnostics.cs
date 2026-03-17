using System.Text;

namespace MawasaProject.Presentation.Diagnostics;

public static class AppDiagnostics
{
    private static readonly object FileLock = new();
    public static string LogPath => Path.Combine(FileSystem.AppDataDirectory, "mawasa.crash.log");

    public static void LogException(string context, Exception exception)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.UtcNow:O}] {context}");
            builder.AppendLine(exception.ToString());
            builder.AppendLine();
            AppendLog(builder.ToString());
        }
        catch
        {
        }
    }

    public static void LogMessage(string message)
    {
        try
        {
            AppendLog($"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void AppendLog(string content)
    {
        lock (FileLock)
        {
            File.AppendAllText(LogPath, content);
        }
    }
}
