using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.Mappers;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class CustomerRepository(
    ISqliteConnectionManager connectionManager,
    SqliteDatabaseOptions options,
    IAppLogger<CustomerRepository> logger)
    : GenericRepository<Customer>(connectionManager, options), ICustomerRepository
{
    protected override string TableName => "Customers";
    protected override string GetByIdSql => "SELECT * FROM Customers WHERE Id = $Id AND IsDeleted = 0 LIMIT 1;";
    protected override string ListSql => "SELECT * FROM Customers WHERE IsDeleted = 0 ORDER BY Name;";
    protected override string DeleteSql => "UPDATE Customers SET IsDeleted = 1, DeletedAtUtc = $DeletedAtUtc WHERE Id = $Id;";

    protected override string InsertSql =>
        "INSERT INTO Customers (Id, Name, PhoneNumber, Email, Address, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc) VALUES ($Id, $Name, $PhoneNumber, $Email, $Address, $CreatedAtUtc, $UpdatedAtUtc, $IsDeleted, $DeletedAtUtc);";

    protected override string UpdateSql =>
        "UPDATE Customers SET Name = $Name, PhoneNumber = $PhoneNumber, Email = $Email, Address = $Address, UpdatedAtUtc = $UpdatedAtUtc, IsDeleted = $IsDeleted, DeletedAtUtc = $DeletedAtUtc WHERE Id = $Id;";

    protected override Customer Map(SqliteDataReader reader)
    {
        return CustomerMapper.FromReader(reader);
    }

    protected override void BindInsert(SqliteCommand command, Customer entity)
    {
        command.Parameters.AddWithValue("$Id", entity.Id.ToString());
        command.Parameters.AddWithValue("$Name", entity.Name);
        command.Parameters.AddWithValue("$PhoneNumber", (object?)entity.PhoneNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$Email", (object?)entity.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("$Address", (object?)entity.Address ?? DBNull.Value);
        command.Parameters.AddWithValue("$CreatedAtUtc", entity.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$UpdatedAtUtc", (object?)entity.UpdatedAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$IsDeleted", entity.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("$DeletedAtUtc", (object?)entity.DeletedAtUtc?.ToString("O") ?? DBNull.Value);
    }

    protected override void BindUpdate(SqliteCommand command, Customer entity)
    {
        BindInsert(command, entity);
    }

    public async Task<IReadOnlyList<Customer>> SearchAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return await ListAsync(cancellationToken);
        }

        const string sql = """
            SELECT * FROM Customers
            WHERE IsDeleted = 0
              AND (Name LIKE $Search OR PhoneNumber LIKE $Search OR Email LIKE $Search)
            ORDER BY Name;
            """;

        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = CreateCommand(connection, sql);
            command.Parameters.AddWithValue("$Search", "%" + searchText.Trim() + "%");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<Customer>();
            while (await reader.ReadAsync(cancellationToken))
            {
                output.Add(Map(reader));
            }

            return output;
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to search customers by text {0}", searchText);
            throw CreateRepositoryException("Search", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = CreateCommand(connection, DeleteSql);
            command.Parameters.AddWithValue("$Id", id.ToString());
            command.Parameters.AddWithValue("$DeletedAtUtc", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to soft delete customer {0}", id);
            throw CreateRepositoryException("Delete", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }
}
