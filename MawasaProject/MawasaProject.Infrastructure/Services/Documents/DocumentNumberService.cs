using Microsoft.Data.Sqlite;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class DocumentNumberService(ISqliteConnectionManager connectionManager)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string> NextAsync(string sequenceName, string prefix, CancellationToken cancellationToken = default)
    {
        var dateKey = DateTime.UtcNow.ToString("yyyyMMdd");
        var rowKey = $"{sequenceName}:{dateKey}";

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
            try
            {
                using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                var current = 0;
                using (var select = connection.CreateCommand())
                {
                    select.Transaction = (SqliteTransaction)transaction;
                    select.CommandText = "SELECT CurrentValue FROM DocumentSequences WHERE Name = $Name LIMIT 1;";
                    select.Parameters.AddWithValue("$Name", rowKey);
                    var result = await select.ExecuteScalarAsync(cancellationToken);
                    if (result is not null && result is not DBNull)
                    {
                        current = Convert.ToInt32(result);
                    }
                }

                var next = current + 1;
                if (current == 0)
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = (SqliteTransaction)transaction;
                    insert.CommandText = "INSERT INTO DocumentSequences (Name, CurrentValue, UpdatedAtUtc) VALUES ($Name, $CurrentValue, $UpdatedAtUtc);";
                    insert.Parameters.AddWithValue("$Name", rowKey);
                    insert.Parameters.AddWithValue("$CurrentValue", next);
                    insert.Parameters.AddWithValue("$UpdatedAtUtc", DateTime.UtcNow.ToString("O"));
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    using var update = connection.CreateCommand();
                    update.Transaction = (SqliteTransaction)transaction;
                    update.CommandText = "UPDATE DocumentSequences SET CurrentValue = $CurrentValue, UpdatedAtUtc = $UpdatedAtUtc WHERE Name = $Name;";
                    update.Parameters.AddWithValue("$Name", rowKey);
                    update.Parameters.AddWithValue("$CurrentValue", next);
                    update.Parameters.AddWithValue("$UpdatedAtUtc", DateTime.UtcNow.ToString("O"));
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return $"{prefix}-{dateKey}-{next:D5}";
            }
            finally
            {
                await connectionManager.DisposeConnectionIfNeededAsync(connection);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
