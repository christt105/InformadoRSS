using System.Text.Json;
using InformadoRSS.Core;

namespace InformadoRSS.Storage;

public sealed class SqliteAlertStateStore(SqliteDatabase database) : IAlertStateStore
{
    public async Task<FeedAlertState?> GetFeedStateAsync(long feedId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT failing_since, alerted_at FROM feed_alerts WHERE feed_id = $feedId";
        command.Parameters.AddWithValue("$feedId", feedId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new FeedAlertState(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)), SqliteValues.GetTimestamp(reader, 1))
            : null;
    }

    public async Task SetFeedStateAsync(long feedId, FeedAlertState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO feed_alerts (feed_id, failing_since, alerted_at)
            SELECT $feedId, $failingSince, $alertedAt WHERE EXISTS (SELECT 1 FROM feeds WHERE id = $feedId)
            ON CONFLICT (feed_id) DO UPDATE SET failing_since = excluded.failing_since, alerted_at = excluded.alerted_at
            """;
        command.Parameters.AddWithValue("$feedId", feedId);
        command.Parameters.AddWithValue("$failingSince", state.FailingSince.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$alertedAt", (object?)state.AlertedAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearFeedStateAsync(long feedId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM feed_alerts WHERE feed_id = $feedId";
        command.Parameters.AddWithValue("$feedId", feedId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DestinationAlertState>> ListDestinationStatesAsync(long feedId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.id, r.id, r.name, d.position, d.sink, d.parameters, d.broken_since, d.broken_reason, a.alerted_at
            FROM destinations d
            JOIN routes r ON r.id = d.route_id
            LEFT JOIN destination_alerts a ON a.destination_id = d.id
            WHERE r.feed_id = $feedId AND (d.broken_since IS NOT NULL OR a.destination_id IS NOT NULL)
            ORDER BY r.id, d.position
            """;
        command.Parameters.AddWithValue("$feedId", feedId);

        var states = new List<DestinationAlertState>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            states.Add(new DestinationAlertState(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetInt32(3),
                new Destination(
                    reader.GetString(4),
                    JsonSerializer.Deserialize(reader.GetString(5), ColumnJsonContext.Default.DictionaryStringString) ?? []),
                SqliteValues.GetTimestamp(reader, 6),
                SqliteValues.GetString(reader, 7),
                SqliteValues.GetTimestamp(reader, 8)));
        }

        return states;
    }

    public async Task SetDestinationAlertedAsync(long destinationId, DateTimeOffset? at, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = at is null
            ? "DELETE FROM destination_alerts WHERE destination_id = $destinationId"
            : """
              INSERT INTO destination_alerts (destination_id, alerted_at)
              SELECT $destinationId, $at WHERE EXISTS (SELECT 1 FROM destinations WHERE id = $destinationId)
              ON CONFLICT (destination_id) DO UPDATE SET alerted_at = excluded.alerted_at
              """;
        command.Parameters.AddWithValue("$destinationId", destinationId);
        command.Parameters.AddWithValue("$at", (object?)at?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
