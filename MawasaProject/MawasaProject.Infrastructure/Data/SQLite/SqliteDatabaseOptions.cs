namespace MawasaProject.Infrastructure.Data.SQLite;

public sealed class SqliteDatabaseOptions
{
    public string DatabasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mawasa.db3");
    public bool EnableForeignKeys { get; set; } = true;
    public bool EnableWriteAheadLog { get; set; } = true;
    public string SynchronousMode { get; set; } = "NORMAL";
    public string TempStore { get; set; } = "MEMORY";
    public int BusyTimeoutMs { get; set; } = 5000;
    public int DefaultCommandTimeoutSeconds { get; set; } = 30;
    public int CacheSizeKiB { get; set; } = 65536;
    public int MaxRetryCount { get; set; } = 5;
    public int BaseRetryDelayMs { get; set; } = 80;
}
