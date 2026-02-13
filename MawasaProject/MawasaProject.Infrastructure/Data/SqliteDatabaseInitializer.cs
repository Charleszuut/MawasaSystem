using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using MawasaProject.Application.Abstractions.Security;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Infrastructure.Data.Migrations;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Data;

public sealed class SqliteDatabaseInitializer(
    ISqliteConnectionManager connectionManager,
    IPasswordHasher passwordHasher,
    SqliteDatabaseOptions options) : IDatabaseInitializer
{
    private static readonly SemaphoreSlim InitializeLock = new(1, 1);

    private static readonly IReadOnlyList<DatabaseMigration> Migrations =
    [
        new("001", "MawasaProject.Infrastructure.Data.Sql.001_initial_schema.sql", "Initial core schema"),
        new("002", "MawasaProject.Infrastructure.Data.Sql.002_enterprise_extensions.sql", "Enterprise extension schema"),
        new("003", "MawasaProject.Infrastructure.Data.Sql.003_phase1_hardening.sql", "Phase 1 hardening")
    ];

    private static readonly Guid AdminRoleId = Guid.Parse("7D089763-EBB9-4F5D-909F-02CB685EF65D");
    private static readonly Guid StaffRoleId = Guid.Parse("83CA95EC-56D3-4627-A1DC-F0081B8CC8C8");
    private static readonly Guid AdminUserId = Guid.Parse("9B9B34E1-9F8A-4BD5-B8FB-1DF7A1724E21");
    private static readonly Guid SampleCustomerId = Guid.Parse("5AC1FF84-7785-47E3-B1B8-66CCFF2D8EC0");
    private static readonly Guid SampleBillId = Guid.Parse("053D5168-D69A-49A2-9F8F-D55D78267B62");
    private static readonly Guid SamplePaymentId = Guid.Parse("E2F0F58D-7677-4B4A-B1C9-9ACF5A060D5D");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializeLock.WaitAsync(cancellationToken);

        try
        {
            var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);

            try
            {
                await EnsureMigrationTableAsync(connection, cancellationToken);
                var appliedMigrationCount = 0;

                foreach (var migration in Migrations)
                {
                    var applied = await ApplyMigrationAsync(connection, migration, cancellationToken);
                    if (applied)
                    {
                        appliedMigrationCount++;
                    }
                }

                if (appliedMigrationCount > 0)
                {
                    await ValidateDatabaseIntegrityAsync(connection, cancellationToken);
                }

                await SeedAsync(connection, cancellationToken);
            }
            finally
            {
                await connectionManager.DisposeConnectionIfNeededAsync(connection);
            }
        }
        finally
        {
            InitializeLock.Release();
        }
    }

    private async Task EnsureMigrationTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS SchemaMigrations (
                Version TEXT PRIMARY KEY,
                Name TEXT NULL,
                Checksum TEXT NULL,
                AppliedAtUtc TEXT NOT NULL
            );
            """;

        using var command = connection.CreateCommand();
        command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        command.CommandText = createSql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "SchemaMigrations", "Name", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "SchemaMigrations", "Checksum", "TEXT NULL", cancellationToken);
    }

    private async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        using var infoCommand = connection.CreateCommand();
        infoCommand.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        infoCommand.CommandText = $"PRAGMA table_info({table});";

        using var reader = await infoCommand.ExecuteReaderAsync(cancellationToken);
        var exists = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }

        await reader.DisposeAsync();

        if (exists)
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> ApplyMigrationAsync(SqliteConnection connection, DatabaseMigration migration, CancellationToken cancellationToken)
    {
        var script = await LoadEmbeddedSqlAsync(migration.ResourceName, cancellationToken);
        var checksum = ComputeChecksum(script);

        var existing = await GetAppliedMigrationAsync(connection, migration.Version, cancellationToken);
        if (existing is { } applied)
        {
            if (!string.IsNullOrWhiteSpace(applied.Checksum)
                && !string.Equals(applied.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Migration {migration.Version} checksum mismatch. Expected {applied.Checksum}, got {checksum}.");
            }

            return false;
        }

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandTimeout = options.DefaultCommandTimeoutSeconds;
            insert.CommandText = "INSERT INTO SchemaMigrations (Version, Name, Checksum, AppliedAtUtc) VALUES ($Version, $Name, $Checksum, $AppliedAtUtc);";
            insert.Parameters.AddWithValue("$Version", migration.Version);
            insert.Parameters.AddWithValue("$Name", migration.Name);
            insert.Parameters.AddWithValue("$Checksum", checksum);
            insert.Parameters.AddWithValue("$AppliedAtUtc", DateTime.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<(string Version, string? Checksum)?> GetAppliedMigrationAsync(SqliteConnection connection, string version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        command.CommandText = "SELECT Version, Checksum FROM SchemaMigrations WHERE Version = $Version LIMIT 1;";
        command.Parameters.AddWithValue("$Version", version);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static async Task<string> LoadEmbeddedSqlAsync(string resourceName, CancellationToken cancellationToken)
    {
        await Task.Yield();

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private async Task ValidateDatabaseIntegrityAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using (var quickCheck = connection.CreateCommand())
        {
            quickCheck.CommandTimeout = options.DefaultCommandTimeoutSeconds;
            quickCheck.CommandText = "PRAGMA quick_check;";
            var result = (await quickCheck.ExecuteScalarAsync(cancellationToken))?.ToString();

            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite quick_check failed: {result}");
            }
        }

        using var fkCheck = connection.CreateCommand();
        fkCheck.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        fkCheck.CommandText = "PRAGMA foreign_key_check;";

        using var reader = await fkCheck.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.IsDBNull(0) ? "unknown" : reader.GetString(0);
            var rowId = reader.IsDBNull(1) ? "unknown" : reader.GetValue(1).ToString();
            throw new InvalidOperationException($"Foreign key integrity violation detected in table {table}, rowid {rowId}.");
        }
    }

    private async Task SeedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.ToString("O");
        var password = passwordHasher.Hash("Admin@123");

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO Roles (Id, Name, Description, CreatedAtUtc, IsDeleted) VALUES ($Id, $Name, $Description, $CreatedAtUtc, 0);",
            [
                ("$Id", AdminRoleId.ToString()),
                ("$Name", "Admin"),
                ("$Description", "System administrator"),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO Roles (Id, Name, Description, CreatedAtUtc, IsDeleted) VALUES ($Id, $Name, $Description, $CreatedAtUtc, 0);",
            [
                ("$Id", StaffRoleId.ToString()),
                ("$Name", "Staff"),
                ("$Description", "Operational staff"),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        var resolvedAdminRoleId = await QueryGuidAsync(
            connection,
            transaction,
            "SELECT Id FROM Roles WHERE Name = $Name LIMIT 1;",
            [("$Name", "Admin")],
            cancellationToken) ?? AdminRoleId;

        var resolvedStaffRoleId = await QueryGuidAsync(
            connection,
            transaction,
            "SELECT Id FROM Roles WHERE Name = $Name LIMIT 1;",
            [("$Name", "Staff")],
            cancellationToken) ?? StaffRoleId;

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO Users (Id, Username, PasswordHash, PasswordSalt, IsActive, CreatedAtUtc, IsDeleted) VALUES ($Id, $Username, $PasswordHash, $PasswordSalt, 1, $CreatedAtUtc, 0);",
            [
                ("$Id", AdminUserId.ToString()),
                ("$Username", "admin"),
                ("$PasswordHash", password.Hash),
                ("$PasswordSalt", password.Salt),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        var resolvedAdminUserId = await QueryGuidAsync(
            connection,
            transaction,
            "SELECT Id FROM Users WHERE Username = $Username LIMIT 1;",
            [("$Username", "admin")],
            cancellationToken) ?? AdminUserId;

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO UserRoles (Id, UserId, RoleId, CreatedAtUtc) VALUES ($Id, $UserId, $RoleId, $CreatedAtUtc);",
            [
                ("$Id", Guid.NewGuid().ToString()),
                ("$UserId", resolvedAdminUserId.ToString()),
                ("$RoleId", resolvedAdminRoleId.ToString()),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO UserRoles (Id, UserId, RoleId, CreatedAtUtc) VALUES ($Id, $UserId, $RoleId, $CreatedAtUtc);",
            [
                ("$Id", Guid.NewGuid().ToString()),
                ("$UserId", resolvedAdminUserId.ToString()),
                ("$RoleId", resolvedStaffRoleId.ToString()),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO Customers (Id, Name, PhoneNumber, Email, Address, CreatedAtUtc, IsDeleted) VALUES ($Id, $Name, $Phone, $Email, $Address, $CreatedAtUtc, 0);",
            [
                ("$Id", SampleCustomerId.ToString()),
                ("$Name", "Sample Customer"),
                ("$Phone", "+1-555-0100"),
                ("$Email", "customer@example.com"),
                ("$Address", "101 Main St"),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        var resolvedCustomerId = await QueryGuidAsync(
            connection,
            transaction,
            "SELECT Id FROM Customers WHERE Email = $Email OR Name = $Name LIMIT 1;",
            [
                ("$Email", "customer@example.com"),
                ("$Name", "Sample Customer")
            ],
            cancellationToken) ?? SampleCustomerId;

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO Bills (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, Status, CreatedByUserId, CreatedAtUtc, IsDeleted) VALUES ($Id, $CustomerId, $BillNumber, $Amount, $Balance, $DueDateUtc, $Status, $CreatedByUserId, $CreatedAtUtc, 0);",
            [
                ("$Id", SampleBillId.ToString()),
                ("$CustomerId", resolvedCustomerId.ToString()),
                ("$BillNumber", "BILL-1001"),
                ("$Amount", 150m),
                ("$Balance", 50m),
                ("$DueDateUtc", DateTime.UtcNow.AddDays(10).ToString("O")),
                ("$Status", 1),
                ("$CreatedByUserId", resolvedAdminUserId.ToString()),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        var resolvedBillId = await QueryGuidAsync(
            connection,
            transaction,
            "SELECT Id FROM Bills WHERE BillNumber = $BillNumber LIMIT 1;",
            [("$BillNumber", "BILL-1001")],
            cancellationToken) ?? SampleBillId;

        await ExecuteAsync(connection, transaction,
            "INSERT OR IGNORE INTO Payments (Id, BillId, Amount, PaymentDateUtc, Status, ReferenceNumber, CreatedByUserId, CreatedAtUtc) VALUES ($Id, $BillId, $Amount, $PaymentDateUtc, $Status, $Reference, $CreatedByUserId, $CreatedAtUtc);",
            [
                ("$Id", SamplePaymentId.ToString()),
                ("$BillId", resolvedBillId.ToString()),
                ("$Amount", 100m),
                ("$PaymentDateUtc", now),
                ("$Status", 2),
                ("$Reference", "PMT-1001"),
                ("$CreatedByUserId", resolvedAdminUserId.ToString()),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ExecuteAsync(
        SqliteConnection connection,
        DbTransaction transaction,
        string sql,
        IReadOnlyList<(string Key, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        command.CommandText = sql;

        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<Guid?> QueryGuidAsync(
        SqliteConnection connection,
        DbTransaction transaction,
        string sql,
        IReadOnlyList<(string Key, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
        command.CommandText = sql;

        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            return null;
        }

        if (result is string s && Guid.TryParse(s, out var id))
        {
            return id;
        }

        return Guid.TryParse(result.ToString(), out var parsed) ? parsed : null;
    }

    private static string ComputeChecksum(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
