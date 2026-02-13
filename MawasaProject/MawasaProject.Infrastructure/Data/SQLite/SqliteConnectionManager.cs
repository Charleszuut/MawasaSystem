using System.Threading;
using Microsoft.Data.Sqlite;

namespace MawasaProject.Infrastructure.Data.SQLite;

public sealed class SqliteConnectionManager(SqliteDatabaseOptions options) : ISqliteConnectionManager
{
    private readonly AsyncLocal<SqliteConnection?> _ambientConnection = new();
    private readonly AsyncLocal<SqliteTransaction?> _ambientTransaction = new();

    public SqliteTransaction? CurrentTransaction => _ambientTransaction.Value;

    public bool HasActiveTransaction => _ambientTransaction.Value is not null;

    public string DatabasePath => options.DatabasePath;

    public async Task<SqliteConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var ambient = _ambientConnection.Value;
        if (ambient is not null)
        {
            return ambient;
        }

        EnsureDatabaseDirectory();
        var connection = CreateConnection();

        try
        {
            await SqliteRetryPolicy.ExecuteAsync(ct => connection.OpenAsync(ct), options, cancellationToken);
            await ConfigureConnectionAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeConnectionIfNeededAsync(SqliteConnection connection)
    {
        if (_ambientConnection.Value is not null && ReferenceEquals(connection, _ambientConnection.Value))
        {
            return;
        }

        await connection.DisposeAsync();
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_ambientTransaction.Value is not null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }

        EnsureDatabaseDirectory();
        var connection = CreateConnection();

        try
        {
            await SqliteRetryPolicy.ExecuteAsync(ct => connection.OpenAsync(ct), options, cancellationToken);
            await ConfigureConnectionAsync(connection, cancellationToken);

            var transaction = (SqliteTransaction)await SqliteRetryPolicy.ExecuteAsync(
                ct => connection.BeginTransactionAsync(ct).AsTask(),
                options,
                cancellationToken);

            _ambientConnection.Value = connection;
            _ambientTransaction.Value = transaction;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_ambientTransaction.Value is null || _ambientConnection.Value is null)
        {
            return;
        }

        await _ambientTransaction.Value.CommitAsync(cancellationToken);
        await _ambientTransaction.Value.DisposeAsync();
        await _ambientConnection.Value.DisposeAsync();
        _ambientTransaction.Value = null;
        _ambientConnection.Value = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_ambientTransaction.Value is null || _ambientConnection.Value is null)
        {
            return;
        }

        await _ambientTransaction.Value.RollbackAsync(cancellationToken);
        await _ambientTransaction.Value.DisposeAsync();
        await _ambientConnection.Value.DisposeAsync();
        _ambientTransaction.Value = null;
        _ambientConnection.Value = null;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = options.EnableForeignKeys,
            Pooling = true,
            RecursiveTriggers = false,
            DefaultTimeout = options.DefaultCommandTimeoutSeconds
        };

        return new SqliteConnection(builder.ConnectionString);
    }

    private void EnsureDatabaseDirectory()
    {
        var directory = Path.GetDirectoryName(options.DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task ConfigureConnectionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var pragmas = new List<string>
        {
            $"PRAGMA foreign_keys = {(options.EnableForeignKeys ? "ON" : "OFF")};",
            $"PRAGMA busy_timeout = {options.BusyTimeoutMs};",
            $"PRAGMA synchronous = {options.SynchronousMode};",
            $"PRAGMA temp_store = {options.TempStore};",
            $"PRAGMA cache_size = -{Math.Max(1024, options.CacheSizeKiB)};"
        };

        if (options.EnableWriteAheadLog)
        {
            pragmas.Add("PRAGMA journal_mode = WAL;");
            pragmas.Add("PRAGMA wal_autocheckpoint = 1000;");
        }

        using var command = connection.CreateCommand();
        command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        command.CommandText = string.Join(" ", pragmas);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
