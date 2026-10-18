using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

public sealed class SqliteSeenItemStore(SqliteDatabase database) : ISeenItemStore
{
    public async Task<bool> IsSeededAsync(long routeId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM routes WHERE id = $routeId AND seeded_at IS NOT NULL)";
        command.Parameters.AddWithValue("$routeId", routeId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))! == 1;
    }

    public async Task<int> SeedAsync(long routeId, IEnumerable<string> itemIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var inserted = await InsertSeenAsync(connection, transaction, routeId, itemIds, at, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE routes SET seeded_at = $at WHERE id = $routeId AND seeded_at IS NULL";
            command.Parameters.AddWithValue("$routeId", routeId);
            command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
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
        var ids = itemIds as IReadOnlyCollection<string> ?? itemIds.ToList();
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var inserted = await InsertSeenAsync(connection, transaction, routeId, ids, at, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM destination_items
                WHERE item_id = $itemId AND destination_id IN (SELECT id FROM destinations WHERE route_id = $routeId)
                """;
            command.Parameters.AddWithValue("$routeId", routeId);
            var itemId = command.Parameters.Add("$itemId", SqliteType.Text);
            foreach (var id in ids)
            {
                itemId.Value = id;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task<IReadOnlySet<int>> ListDoneDestinationsAsync(long routeId, string itemId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.position FROM destination_items i JOIN destinations d ON d.id = i.destination_id
            WHERE d.route_id = $routeId AND i.item_id = $itemId
            """;
        command.Parameters.AddWithValue("$routeId", routeId);
        command.Parameters.AddWithValue("$itemId", itemId);

        var positions = new HashSet<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            positions.Add(reader.GetInt32(0));
        }

        return positions;
    }

    public async Task MarkDestinationDoneAsync(
        long routeId, int position, string itemId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO destination_items (destination_id, item_id, done_at)
            SELECT id, $itemId, $at FROM destinations WHERE route_id = $routeId AND position = $position
            """;
        command.Parameters.AddWithValue("$routeId", routeId);
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertSeenAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long routeId,
        IEnumerable<string> itemIds,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
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

        return inserted;
    }
}
