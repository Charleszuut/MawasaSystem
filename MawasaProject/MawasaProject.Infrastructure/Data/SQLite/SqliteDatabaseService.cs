using Microsoft.Data.Sqlite;

namespace MawasaProject.Infrastructure.Data.SQLite;

public sealed class SqliteDatabaseService(
    ISqliteConnectionManager connectionManager,
    SqliteDatabaseOptions options)
{
    public async Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = BuildCommand(connection, sql, parameters);
            return await SqliteRetryPolicy.ExecuteAsync(
                ct => command.ExecuteNonQueryAsync(ct),
                options,
                cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = BuildCommand(connection, sql, parameters);
            var value = await SqliteRetryPolicy.ExecuteAsync(
                ct => command.ExecuteScalarAsync(ct),
                options,
                cancellationToken);

            if (value is null || value is DBNull)
            {
                return default;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqliteDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = BuildCommand(connection, sql, parameters);
            using var reader = await SqliteRetryPolicy.ExecuteAsync(
                ct => command.ExecuteReaderAsync(ct),
                options,
                cancellationToken);

            var output = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(map(reader));
            }

            return output;
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private SqliteCommand BuildCommand(SqliteConnection connection, string sql, IReadOnlyDictionary<string, object?>? parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = connectionManager.CurrentTransaction;
        command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        command.CommandText = sql;

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                SqliteHelper.AddParameter(command, parameter.Key, parameter.Value);
            }
        }

        return command;
    }
}
