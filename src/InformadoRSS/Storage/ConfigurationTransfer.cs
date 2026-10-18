using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

/// <summary>Exports and imports the whole configuration as JSON.</summary>
public sealed class ConfigurationTransfer(SqliteDatabase database, IFeedStore feeds, IRouteStore routes, ISettingsStore settings)
{
    public async Task<ConfigurationSnapshot> ExportAsync(CancellationToken cancellationToken)
    {
        var feedSnapshots = new List<FeedSnapshot>();
        foreach (var feed in await feeds.ListAsync(cancellationToken))
        {
            var routeSnapshots = (await routes.ListByFeedAsync(feed.Id, cancellationToken))
                .Select(r => new RouteSnapshot(
                    r.Settings.Name,
                    r.Settings.Enabled,
                    r.Settings.Destinations,
                    ParseJson(r.Settings.FilterJson),
                    ParseJson(r.Settings.TemplateJson)))
                .ToList();
            feedSnapshots.Add(new FeedSnapshot(
                feed.Settings.Url, feed.Settings.Name, (long)feed.Settings.Interval.TotalSeconds, routeSnapshots));
        }

        return new ConfigurationSnapshot(
            ConfigurationSnapshot.CurrentVersion, feedSnapshots, await settings.ListAsync(cancellationToken));
    }

    /// <summary>
    /// Replaces the whole configuration with <paramref name="snapshot"/> in a single transaction.
    /// Seen items and feed health are discarded, so every route starts again from its first fetch.
    /// </summary>
    /// <exception cref="InvalidOperationException">The snapshot comes from a newer, unsupported format.</exception>
    public async Task ImportAsync(ConfigurationSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Version > ConfigurationSnapshot.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Configuration version {snapshot.Version} is newer than the supported version {ConfigurationSnapshot.CurrentVersion}.");
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM feeds; DELETE FROM settings;";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var feed in snapshot.Feeds)
        {
            var feedId = await SqliteFeedStore.InsertAsync(
                connection,
                transaction,
                new FeedSettings(feed.Url, feed.Name, TimeSpan.FromSeconds(feed.IntervalSeconds)),
                cancellationToken);

            foreach (var route in feed.Routes)
            {
                await SqliteRouteStore.InsertAsync(
                    connection,
                    transaction,
                    feedId,
                    new RouteSettings(
                        route.Name, route.Filter?.GetRawText(), route.Template?.GetRawText(), route.Enabled, route.Destinations),
                    cancellationToken);
            }
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO settings (key, value) VALUES ($key, $value)";
            var key = insert.Parameters.Add("$key", SqliteType.Text);
            var value = insert.Parameters.Add("$value", SqliteType.Text);
            foreach (var setting in snapshot.Settings)
            {
                key.Value = setting.Key;
                value.Value = setting.Value;
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken) =>
        await JsonSerializer.SerializeAsync(
            destination, await ExportAsync(cancellationToken), StorageJsonContext.Default.ConfigurationSnapshot, cancellationToken);

    /// <exception cref="JsonException">The JSON is malformed or misses required fields.</exception>
    public async Task ImportAsync(Stream source, CancellationToken cancellationToken)
    {
        var snapshot = await JsonSerializer.DeserializeAsync(source, StorageJsonContext.Default.ConfigurationSnapshot, cancellationToken)
            ?? throw new JsonException("The configuration document is null.");
        await ImportAsync(snapshot, cancellationToken);
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
