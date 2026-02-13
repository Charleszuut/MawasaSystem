using Microsoft.Data.Sqlite;

namespace MawasaProject.Infrastructure.Data.SQLite;

internal static class SqliteRetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, SqliteDatabaseOptions options, CancellationToken cancellationToken)
    {
        Exception? last = null;

        for (var attempt = 1; attempt <= options.MaxRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken);
            }
            catch (SqliteException ex) when (IsTransient(ex) && attempt < options.MaxRetryCount)
            {
                last = ex;
                var delay = TimeSpan.FromMilliseconds(options.BaseRetryDelayMs * attempt * attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException("SQLite retry policy failed without a captured exception.");
    }

    public static async Task ExecuteAsync(Func<CancellationToken, Task> action, SqliteDatabaseOptions options, CancellationToken cancellationToken)
    {
        await ExecuteAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, options, cancellationToken);
    }

    private static bool IsTransient(SqliteException exception)
    {
        // 5: SQLITE_BUSY, 6: SQLITE_LOCKED
        return exception.SqliteErrorCode is 5 or 6;
    }
}
