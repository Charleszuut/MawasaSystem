using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class CustomerRepository(ISqliteConnectionManager connectionManager)
    : GenericRepository<Customer>(connectionManager), ICustomerRepository
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
        return new Customer
        {
            Id = SqliteHelper.GetGuid(reader, "Id"),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber")),
            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
            Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? null : reader.GetString(reader.GetOrdinal("Address")),
            CreatedAtUtc = SqliteHelper.GetDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = SqliteHelper.GetNullableDateTime(reader, "UpdatedAtUtc"),
            IsDeleted = SqliteHelper.GetBoolean(reader, "IsDeleted"),
            DeletedAtUtc = SqliteHelper.GetNullableDateTime(reader, "DeletedAtUtc")
        };
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
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$Search", "%" + searchText.Trim() + "%");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var output = new List<Customer>();
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

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = ConnectionManager.CurrentTransaction;
            command.CommandText = DeleteSql;
            command.Parameters.AddWithValue("$Id", id.ToString());
            command.Parameters.AddWithValue("$DeletedAtUtc", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }
}
