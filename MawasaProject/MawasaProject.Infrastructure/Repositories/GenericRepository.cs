using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.Interfaces;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public abstract class GenericRepository<T>(ISqliteConnectionManager connectionManager) : IRepository<T>
    where T : class, IEntity, new()
{
    protected ISqliteConnectionManager ConnectionManager => connectionManager;

    protected abstract string TableName { get; }
    protected virtual string GetByIdSql => $"SELECT * FROM {TableName} WHERE Id = $Id LIMIT 1;";
    protected virtual string ListSql => $"SELECT * FROM {TableName};";
    protected virtual string DeleteSql => $"DELETE FROM {TableName} WHERE Id = $Id;";

    protected abstract string InsertSql { get; }
    protected abstract string UpdateSql { get; }

    protected abstract T Map(SqliteDataReader reader);
    protected abstract void BindInsert(SqliteCommand command, T entity);
    protected abstract void BindUpdate(SqliteCommand command, T entity);

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = GetByIdSql;
            command.Parameters.AddWithValue("$Id", id.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = ListSql;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(Map(reader));
            }

            return output;
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = InsertSql;
            BindInsert(command, entity);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = UpdateSql;
            BindUpdate(command, entity);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = DeleteSql;
            command.Parameters.AddWithValue("$Id", id.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }
}
