using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace MawasaProject.Infrastructure.Services.Backup;

public sealed class BackupIntegrityChecker
{
    public string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public async Task<bool> ValidateSqliteIntegrityAsync(string sqliteFilePath, CancellationToken cancellationToken = default)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = sqliteFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var quick = connection.CreateCommand())
        {
            quick.CommandText = "PRAGMA quick_check;";
            var result = (await quick.ExecuteScalarAsync(cancellationToken))?.ToString();
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        await using var fk = connection.CreateCommand();
        fk.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await fk.ExecuteReaderAsync(cancellationToken);
        return !await reader.ReadAsync(cancellationToken);
    }
}
