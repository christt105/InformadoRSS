using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

public sealed class SqliteSeenItemStore(SqliteDatabase database) : ISeenItemStore
{
    public async Task<bool> HasAnyAsync(long routeId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM seen_items WHERE route_id = $routeId)";
        command.Parameters.AddWithValue("$routeId", routeId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))! == 1;
    }

    public async Task<IReadOnlyList<string>> FilterUnseenAsync(
        long routeId, IReadOnlyList<string> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM seen_items WHERE route_id = $routeId AND item_id = $itemId)";
        command.Parameters.AddWithValue("$routeId", routeId);
        var itemId = command.Parameters.Add("$itemId", SqliteType.Text);

        var unseen = new List<string>();
        foreach (var id in itemIds)
        {
            itemId.Value = id;
            if ((long)(await command.ExecuteScalarAsync(cancellationToken))! == 0)
            {
                unseen.Add(id);
            }
        }

        return unseen;
    }

    public async Task<int> MarkSeenAsync(
        long routeId, IEnumerable<string> itemIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO seen_items (route_id, item_id, seen_at) VALUES ($routeId, $itemId, $at)";
        command.Parameters.AddWithValue("$routeId", routeId);
        command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
        var itemId = command.Parameters.Add("$itemId", SqliteType.Text);

        var inserted = 0;
        foreach (var id in itemIds)
        {
            itemId.Value = id;
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }
}
