using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

public sealed class SqliteFeedStore(SqliteDatabase database) : IFeedStore
{
    private const string SelectColumns =
        "SELECT id, url, name, interval_seconds, consecutive_failures, last_success_at, last_error_at, last_error, etag, last_modified, identity_pattern FROM feeds";

    /// <summary>How old the stored success time may get before a success that changes nothing else rewrites it.</summary>
    public static TimeSpan SuccessRefreshInterval { get; } = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<Feed>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY name, id";
        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<Feed?> GetAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        var feeds = await ReadAllAsync(command, cancellationToken);
        return feeds.Count == 0 ? null : feeds[0];
    }

    public async Task<Feed> AddAsync(FeedSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var id = await InsertAsync(connection, null, settings, cancellationToken);
        return new Feed(id, settings, FeedHealth.Unknown, FeedValidators.None);
    }

    public async Task<bool> UpdateAsync(long id, FeedSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var reseed = connection.CreateCommand();
        reseed.Transaction = transaction;
        reseed.CommandText = """
            UPDATE routes SET seeded_at = NULL
            WHERE feed_id = $id AND (SELECT identity_pattern IS NOT $identityPattern FROM feeds WHERE id = $id)
            """;
        reseed.Parameters.AddWithValue("$id", id);
        reseed.Parameters.AddWithValue("$identityPattern", SqliteValues.OrDbNull(settings.IdentityPattern));
        await reseed.ExecuteNonQueryAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE feeds
            SET url = $url, name = $name, interval_seconds = $interval, identity_pattern = $identityPattern,
                etag = CASE WHEN url = $url THEN etag END,
                last_modified = CASE WHEN url = $url THEN last_modified END
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        AddSettingsParameters(command, settings);
        try
        {
            var updated = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
            await transaction.CommitAsync(cancellationToken);
            return updated;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrors.ConstraintUnique)
        {
            throw new DuplicateFeedException(settings.Url);
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM feeds WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task RecordSuccessAsync(long id, DateTimeOffset at, FeedValidators validators, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE feeds
            SET consecutive_failures = 0, last_success_at = $at, etag = $etag, last_modified = $lastModified
            WHERE id = $id
                AND (consecutive_failures <> 0 OR last_success_at IS NULL OR last_success_at <= $refreshBefore
                    OR etag IS NOT $etag OR last_modified IS NOT $lastModified)
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$refreshBefore", (at - SuccessRefreshInterval).ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$etag", SqliteValues.OrDbNull(validators.ETag));
        command.Parameters.AddWithValue("$lastModified", SqliteValues.OrDbNull(validators.LastModified));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(long id, DateTimeOffset at, string message, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE feeds
            SET consecutive_failures = consecutive_failures + 1, last_error_at = $at, last_error = $error
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$error", message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task<long> InsertAsync(
        SqliteConnection connection, SqliteTransaction? transaction, FeedSettings settings, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO feeds (url, name, interval_seconds, identity_pattern)
            VALUES ($url, $name, $interval, $identityPattern)
            RETURNING id
            """;
        AddSettingsParameters(command, settings);
        try
        {
            return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrors.ConstraintUnique)
        {
            throw new DuplicateFeedException(settings.Url);
        }
    }

    private static void AddSettingsParameters(SqliteCommand command, FeedSettings settings)
    {
        command.Parameters.AddWithValue("$url", settings.Url.AbsoluteUri);
        command.Parameters.AddWithValue("$name", settings.Name);
        command.Parameters.AddWithValue("$interval", (long)settings.Interval.TotalSeconds);
        command.Parameters.AddWithValue("$identityPattern", SqliteValues.OrDbNull(settings.IdentityPattern));
    }

    private static async Task<IReadOnlyList<Feed>> ReadAllAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var feeds = new List<Feed>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            feeds.Add(new Feed(
                reader.GetInt64(0),
                new FeedSettings(
                    new Uri(reader.GetString(1)),
                    reader.GetString(2),
                    TimeSpan.FromSeconds(reader.GetInt64(3)),
                    SqliteValues.GetString(reader, 10)),
                new FeedHealth(
                    reader.GetInt32(4),
                    SqliteValues.GetTimestamp(reader, 5),
                    SqliteValues.GetTimestamp(reader, 6),
                    SqliteValues.GetString(reader, 7)),
                new FeedValidators(SqliteValues.GetString(reader, 8), SqliteValues.GetString(reader, 9))));
        }

        return feeds;
    }
}
