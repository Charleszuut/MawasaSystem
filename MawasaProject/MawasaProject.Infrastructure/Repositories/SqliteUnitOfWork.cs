using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class SqliteUnitOfWork(ISqliteConnectionManager connectionManager) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await connectionManager.BeginTransactionAsync(cancellationToken);

        try
        {
            await action(cancellationToken);
            await connectionManager.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await connectionManager.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await connectionManager.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await action(cancellationToken);
            await connectionManager.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await connectionManager.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
