using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Logging;
using MawasaProject.Application.Abstractions.Persistence;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Entities;
using MawasaProject.Infrastructure.Data.Mappers;
using MawasaProject.Infrastructure.Data.SQLite;
using UserRole = MawasaProject.Domain.Enums.UserRole;

namespace MawasaProject.Infrastructure.Repositories;

public sealed class UserRepository(
    ISqliteConnectionManager connectionManager,
    SqliteDatabaseOptions options,
    IAppLogger<UserRepository> logger)
    : GenericRepository<User>(connectionManager, options), IUserRepository
{
    protected override string TableName => "Users";
    protected override string GetByIdSql => "SELECT * FROM Users WHERE Id = $Id AND IsDeleted = 0 LIMIT 1;";
    protected override string ListSql => "SELECT * FROM Users WHERE IsDeleted = 0 ORDER BY Username;";
    protected override string DeleteSql => "UPDATE Users SET IsDeleted = 1, DeletedAtUtc = $DeletedAtUtc WHERE Id = $Id;";

    protected override string InsertSql =>
        "INSERT INTO Users (Id, Username, PasswordHash, PasswordSalt, IsActive, LastLoginAtUtc, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc) VALUES ($Id, $Username, $PasswordHash, $PasswordSalt, $IsActive, $LastLoginAtUtc, $CreatedAtUtc, $UpdatedAtUtc, $IsDeleted, $DeletedAtUtc);";

    protected override string UpdateSql =>
        "UPDATE Users SET Username = $Username, PasswordHash = $PasswordHash, PasswordSalt = $PasswordSalt, IsActive = $IsActive, LastLoginAtUtc = $LastLoginAtUtc, UpdatedAtUtc = $UpdatedAtUtc, IsDeleted = $IsDeleted, DeletedAtUtc = $DeletedAtUtc WHERE Id = $Id;";

    protected override User Map(SqliteDataReader reader)
    {
        return UserMapper.FromReader(reader);
    }

    protected override void BindInsert(SqliteCommand command, User entity)
    {
        command.Parameters.AddWithValue("$Id", entity.Id.ToString());
        command.Parameters.AddWithValue("$Username", entity.Username);
        command.Parameters.AddWithValue("$PasswordHash", entity.PasswordHash);
        command.Parameters.AddWithValue("$PasswordSalt", entity.PasswordSalt);
        command.Parameters.AddWithValue("$IsActive", entity.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$LastLoginAtUtc", (object?)entity.LastLoginAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$CreatedAtUtc", entity.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$UpdatedAtUtc", (object?)entity.UpdatedAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$IsDeleted", entity.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("$DeletedAtUtc", (object?)entity.DeletedAtUtc?.ToString("O") ?? DBNull.Value);
    }

    protected override void BindUpdate(SqliteCommand command, User entity)
    {
        BindInsert(command, entity);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = CreateCommand(connection, "SELECT * FROM Users WHERE Username = $Username AND IsDeleted = 0 LIMIT 1;");
            command.Parameters.AddWithValue("$Username", username);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to get user by username {0}", username);
            throw CreateRepositoryException("GetByUsername", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<IReadOnlyList<UserRole>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT r.Name
            FROM UserRoles ur
            INNER JOIN Roles r ON ur.RoleId = r.Id
            WHERE ur.UserId = $UserId AND r.IsDeleted = 0;
            """;

        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var command = CreateCommand(connection, sql);
            command.Parameters.AddWithValue("$UserId", userId.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var roles = new List<UserRole>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                if (Enum.TryParse<UserRole>(name, ignoreCase: true, out var role))
                {
                    roles.Add(role);
                }
            }

            return roles;
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to get roles for user {0}", userId);
            throw CreateRepositoryException("GetUserRoles", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task AssignRolesAsync(Guid userId, IReadOnlyCollection<UserRole> roles, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionManager.GetOpenConnectionAsync(cancellationToken);

        try
        {
            using var delete = CreateCommand(connection, "DELETE FROM UserRoles WHERE UserId = $UserId;");
            delete.Parameters.AddWithValue("$UserId", userId.ToString());
            await delete.ExecuteNonQueryAsync(cancellationToken);

            foreach (var role in roles)
            {
                var roleId = await EnsureRoleExistsAsync(connection, role.ToString(), cancellationToken);

                using var insert = CreateCommand(connection, "INSERT INTO UserRoles (Id, UserId, RoleId, CreatedAtUtc) VALUES ($Id, $UserId, $RoleId, $CreatedAtUtc);");
                insert.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
                insert.Parameters.AddWithValue("$UserId", userId.ToString());
                insert.Parameters.AddWithValue("$RoleId", roleId.ToString());
                insert.Parameters.AddWithValue("$CreatedAtUtc", DateTime.UtcNow.ToString("O"));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (SqliteException exception)
        {
            logger.Error(exception, "Failed to assign roles for user {0}", userId);
            throw CreateRepositoryException("AssignRoles", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersWithRolesAsync(CancellationToken cancellationToken = default)
    {
        var users = await ListAsync(cancellationToken);
        var dtos = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await GetUserRolesAsync(user.Id, cancellationToken);
            dtos.Add(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                IsActive = user.IsActive,
                Roles = roles
            });
        }

        return dtos;
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
            logger.Error(exception, "Failed to soft delete user {0}", id);
            throw CreateRepositoryException("Delete", exception);
        }
        finally
        {
            await ConnectionManager.DisposeConnectionIfNeededAsync(connection);
        }
    }

    private async Task<Guid> EnsureRoleExistsAsync(SqliteConnection connection, string roleName, CancellationToken cancellationToken)
    {
        using var check = CreateCommand(connection, "SELECT Id FROM Roles WHERE Name = $Name LIMIT 1;");
        check.Parameters.AddWithValue("$Name", roleName);
        var existing = await check.ExecuteScalarAsync(cancellationToken);

        if (existing is string value && Guid.TryParse(value, out var id))
        {
            return id;
        }

        var roleId = Guid.NewGuid();
        using var insert = CreateCommand(connection, "INSERT INTO Roles (Id, Name, Description, CreatedAtUtc, IsDeleted) VALUES ($Id, $Name, $Description, $CreatedAtUtc, 0);");
        insert.Parameters.AddWithValue("$Id", roleId.ToString());
        insert.Parameters.AddWithValue("$Name", roleName);
        insert.Parameters.AddWithValue("$Description", roleName + " role");
        insert.Parameters.AddWithValue("$CreatedAtUtc", DateTime.UtcNow.ToString("O"));
        await insert.ExecuteNonQueryAsync(cancellationToken);

        return roleId;
    }
}
