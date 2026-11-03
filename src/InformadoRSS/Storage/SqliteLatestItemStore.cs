using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

public sealed class SqliteLatestItemStore(SqliteDatabase database) : ILatestItemStore
{
    /// <remarks>Does nothing when the feed no longer exists or the item is already stored unchanged.</remarks>
    public async Task SetAsync(long feedId, LatestItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO latest_items (feed_id, item_id, title, link, published_at, fetched_at)
            SELECT $feedId, $itemId, $title, $link, $published, $fetched WHERE EXISTS (SELECT 1 FROM feeds WHERE id = $feedId)
            ON CONFLICT (feed_id) DO UPDATE SET
                item_id = excluded.item_id, title = excluded.title, link = excluded.link,
                published_at = excluded.published_at, fetched_at = excluded.fetched_at
            WHERE item_id IS NOT excluded.item_id OR title IS NOT excluded.title OR link IS NOT excluded.link
                OR published_at IS NOT excluded.published_at
            """;
        command.Parameters.AddWithValue("$feedId", feedId);
        command.Parameters.AddWithValue("$itemId", item.Id);
        command.Parameters.AddWithValue("$title", SqliteValues.OrDbNull(item.Title));
        command.Parameters.AddWithValue("$link", SqliteValues.OrDbNull(item.Link));
        command.Parameters.AddWithValue("$published", (object?)item.Published?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("$fetched", item.FetchedAt.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAsync(long feedId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM latest_items WHERE feed_id = $feedId";
        command.Parameters.AddWithValue("$feedId", feedId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, LatestItem>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT feed_id, item_id, title, link, published_at, fetched_at FROM latest_items";
        var items = new Dictionary<long, LatestItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items[reader.GetInt64(0)] = new LatestItem(
                reader.GetString(1),
                SqliteValues.GetString(reader, 2),
                SqliteValues.GetString(reader, 3),
                SqliteValues.GetTimestamp(reader, 4),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)));
        }

        return items;
    }
}
