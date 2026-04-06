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
        new("003", "MawasaProject.Infrastructure.Data.Sql.003_phase1_hardening.sql", "Phase 1 hardening"),
        new("004", "MawasaProject.Infrastructure.Data.Sql.004_phase6_audit_hardening.sql", "Phase 6 audit hardening"),
        new("005", "MawasaProject.Infrastructure.Data.Sql.005_phase12_backup_hardening.sql", "Phase 12 backup and restore hardening"),
        new("006", "MawasaProject.Infrastructure.Data.Sql.006_phase13_printer_hardening.sql", "Phase 13 printer integration hardening"),
        new("007", "MawasaProject.Infrastructure.Data.Sql.007_phase14_documents_hardening.sql", "Phase 14 receipt and invoice hardening"),
        new("008", "MawasaProject.Infrastructure.Data.Sql.008_billing_table_alias.sql", "Billing table mirror"),
        new("009", "MawasaProject.Infrastructure.Data.Sql.009_relax_billing_constraint.sql", "Relax Billing balance constraint"),
        new("010", "MawasaProject.Infrastructure.Data.Sql.010_customer_disconnection.sql", "Customer disconnection tracking")
    ];

    private static readonly Guid AdminRoleId = Guid.Parse("7D089763-EBB9-4F5D-909F-02CB685EF65D");
    private static readonly Guid StaffRoleId = Guid.Parse("83CA95EC-56D3-4627-A1DC-F0081B8CC8C8");
    private static readonly Guid AdminUserId = Guid.Parse("9B9B34E1-9F8A-4BD5-B8FB-1DF7A1724E21");
    private static readonly Guid StaffUserId = Guid.Parse("6AE79A5F-7CF1-4E2B-98A3-F9E7C090E5F1");


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

                await EnsureBillingMirrorAsync(connection, cancellationToken);
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

        await ExecuteMigrationScriptAsync(connection, (SqliteTransaction)transaction, script, cancellationToken);

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

    private async Task ExecuteMigrationScriptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string script,
        CancellationToken cancellationToken)
    {
        foreach (var statement in SplitSqlStatements(script))
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = options.DefaultCommandTimeoutSeconds;
            command.CommandText = statement;

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (IsIgnorableMigrationError(exception, statement))
            {
                continue;
            }
        }
    }

    private static IReadOnlyList<string> SplitSqlStatements(string script)
    {
        var statements = new List<string>();
        var builder = new StringBuilder();
        var token = new StringBuilder();

        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracket = false;
        var inLineComment = false;
        var inBlockComment = false;

        var triggerMode = false;
        var detectTrigger = true;
        var tokenCount = 0;
        string? firstToken = null;
        string? secondToken = null;
        string? lastToken = null;

        void ResetStatementState()
        {
            builder.Clear();
            token.Clear();
            inSingleQuote = false;
            inDoubleQuote = false;
            inBracket = false;
            inLineComment = false;
            inBlockComment = false;
            triggerMode = false;
            detectTrigger = true;
            tokenCount = 0;
            firstToken = null;
            secondToken = null;
            lastToken = null;
        }

        void ConsiderTriggerToken(string value)
        {
            if (!detectTrigger || triggerMode)
            {
                return;
            }

            tokenCount++;

            if (tokenCount == 1)
            {
                firstToken = value;
                if (!string.Equals(firstToken, "create", StringComparison.OrdinalIgnoreCase))
                {
                    detectTrigger = false;
                }

                return;
            }

            if (tokenCount == 2)
            {
                secondToken = value;
                if (string.Equals(firstToken, "create", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(secondToken, "trigger", StringComparison.OrdinalIgnoreCase))
                    {
                        triggerMode = true;
                        detectTrigger = false;
                    }
                    else if (string.Equals(secondToken, "temp", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(secondToken, "temporary", StringComparison.OrdinalIgnoreCase))
                    {
                        // Wait for third token.
                    }
                    else
                    {
                        detectTrigger = false;
                    }
                }
                else
                {
                    detectTrigger = false;
                }

                return;
            }

            if (tokenCount == 3)
            {
                if (string.Equals(firstToken, "create", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(secondToken, "temp", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(secondToken, "temporary", StringComparison.OrdinalIgnoreCase))
                    && string.Equals(value, "trigger", StringComparison.OrdinalIgnoreCase))
                {
                    triggerMode = true;
                }

                detectTrigger = false;
            }
        }

        void FlushToken()
        {
            if (token.Length == 0)
            {
                return;
            }

            var value = token.ToString();
            lastToken = value;
            ConsiderTriggerToken(value);
            token.Clear();
        }

        for (var i = 0; i < script.Length; i++)
        {
            var c = script[i];
            var next = i + 1 < script.Length ? script[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                    builder.Append(c);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                    builder.Append(' ');
                }

                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && !inBracket)
            {
                if (c == '-' && next == '-')
                {
                    FlushToken();
                    inLineComment = true;
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    FlushToken();
                    inBlockComment = true;
                    builder.Append(' ');
                    i++;
                    continue;
                }
            }

            if (inSingleQuote)
            {
                builder.Append(c);
                if (c == '\'' && next == '\'')
                {
                    builder.Append(next);
                    i++;
                }
                else if (c == '\'')
                {
                    inSingleQuote = false;
                }

                continue;
            }

            if (inDoubleQuote)
            {
                builder.Append(c);
                if (c == '"')
                {
                    inDoubleQuote = false;
                }

                continue;
            }

            if (inBracket)
            {
                builder.Append(c);
                if (c == ']')
                {
                    inBracket = false;
                }

                continue;
            }

            if (c == '\'')
            {
                FlushToken();
                inSingleQuote = true;
                builder.Append(c);
                continue;
            }

            if (c == '"')
            {
                FlushToken();
                inDoubleQuote = true;
                builder.Append(c);
                continue;
            }

            if (c == '[')
            {
                FlushToken();
                inBracket = true;
                builder.Append(c);
                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                token.Append(c);
                builder.Append(c);
                continue;
            }

            FlushToken();

            if (c == ';')
            {
                if (!triggerMode || string.Equals(lastToken, "end", StringComparison.OrdinalIgnoreCase))
                {
                    var statement = builder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        statements.Add(statement);
                    }

                    ResetStatementState();
                    continue;
                }
            }

            builder.Append(c);
        }

        FlushToken();

        var tail = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
        {
            statements.Add(tail);
        }

        return statements;
    }

    private static bool IsIgnorableMigrationError(SqliteException exception, string statement)
    {
        var message = exception.Message ?? string.Empty;
        var normalized = statement.TrimStart();

        if (message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase)
            && normalized.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains(" ADD COLUMN ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            && (normalized.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
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

    private static async Task EnsureBillingMirrorAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string createTable = """
            CREATE TABLE IF NOT EXISTS Billing (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                BillNumber TEXT NOT NULL UNIQUE,
                Amount REAL NOT NULL CHECK(Amount >= 0),
                Balance REAL NOT NULL CHECK(Balance >= 0),
                DueDateUtc TEXT NOT NULL,
                PaidAtUtc TEXT NULL,
                Status INTEGER NOT NULL CHECK(Status IN (1,2,3)),
                CreatedByUserId TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK(IsDeleted IN (0,1)),
                DeletedAtUtc TEXT NULL,
                CONSTRAINT FK_Billing_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE RESTRICT
            );
            """;

        using (var create = connection.CreateCommand())
        {
            create.CommandText = createTable;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        const string backfill = """
            INSERT INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
            SELECT Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc
            FROM Bills
            WHERE Id NOT IN (SELECT Id FROM Billing);
            """;

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = backfill;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertTrigger = """
            CREATE TRIGGER IF NOT EXISTS trg_Bills_Insert_Billing
            AFTER INSERT ON Bills
            BEGIN
                INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
                VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc);
            END;
            """;

        using (var trigger = connection.CreateCommand())
        {
            trigger.CommandText = insertTrigger;
            await trigger.ExecuteNonQueryAsync(cancellationToken);
        }

        const string updateTrigger = """
            CREATE TRIGGER IF NOT EXISTS trg_Bills_Update_Billing
            AFTER UPDATE ON Bills
            BEGIN
                INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
                VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc);
            END;
            """;

        using (var trigger = connection.CreateCommand())
        {
            trigger.CommandText = updateTrigger;
            await trigger.ExecuteNonQueryAsync(cancellationToken);
        }

        const string deleteTrigger = """
            CREATE TRIGGER IF NOT EXISTS trg_Bills_Delete_Billing
            AFTER DELETE ON Bills
            BEGIN
                DELETE FROM Billing WHERE Id = OLD.Id;
            END;
            """;

        using (var trigger = connection.CreateCommand())
        {
            trigger.CommandText = deleteTrigger;
            await trigger.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task SeedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.ToString("O");
        var adminPassword = passwordHasher.Hash("Admin@123");
        var staffPassword = passwordHasher.Hash("Staff@123");

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
                ("$PasswordHash", adminPassword.Hash),
                ("$PasswordSalt", adminPassword.Salt),
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
            "INSERT OR IGNORE INTO Users (Id, Username, PasswordHash, PasswordSalt, IsActive, CreatedAtUtc, IsDeleted) VALUES ($Id, $Username, $PasswordHash, $PasswordSalt, 1, $CreatedAtUtc, 0);",
            [
                ("$Id", StaffUserId.ToString()),
                ("$Username", "staff"),
                ("$PasswordHash", staffPassword.Hash),
                ("$PasswordSalt", staffPassword.Salt),
                ("$CreatedAtUtc", now)
            ],
            cancellationToken);

        var resolvedStaffUserId = await QueryGuidAsync(
            connection,
            transaction,
            "SELECT Id FROM Users WHERE Username = $Username LIMIT 1;",
            [("$Username", "staff")],
            cancellationToken) ?? StaffUserId;

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
                ("$UserId", resolvedStaffUserId.ToString()),
                ("$RoleId", resolvedStaffRoleId.ToString()),
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
