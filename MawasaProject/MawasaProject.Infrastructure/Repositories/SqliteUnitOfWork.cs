using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class SqliteUnitOfWork(
    ISqliteConnectionManager connectionManager,
    IAppLogger<SqliteUnitOfWork> logger) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await connectionManager.BeginTransactionAsync(cancellationToken);

        try
        {
            await action(cancellationToken);
            await connectionManager.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await connectionManager.RollbackTransactionAsync(cancellationToken);
            }
            catch (Exception rollbackException)
            {
                logger.Error(rollbackException, "Rollback failed after transaction error.");
            }

            logger.Error(exception, "Transaction execution failed.");
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
        catch (Exception exception)
        {
            try
            {
                await connectionManager.RollbackTransactionAsync(cancellationToken);
            }
            catch (Exception rollbackException)
            {
                logger.Error(rollbackException, "Rollback failed after transaction error.");
            }

            logger.Error(exception, "Transaction execution failed.");
            throw;
        }
    }
}
