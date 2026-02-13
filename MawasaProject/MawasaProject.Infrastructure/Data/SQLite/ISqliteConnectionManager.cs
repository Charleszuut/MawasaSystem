using Microsoft.Data.Sqlite;

namespace MawasaProject.Infrastructure.Data.SQLite;

public interface ISqliteConnectionManager
{
    SqliteTransaction? CurrentTransaction { get; }
    bool HasActiveTransaction { get; }
    string DatabasePath { get; }
    Task<SqliteConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
    Task DisposeConnectionIfNeededAsync(SqliteConnection connection);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
