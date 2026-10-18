using System.Text.Json;
using InformadoRSS.Core;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

public sealed class SqliteRouteStore(SqliteDatabase database) : IRouteStore
{
    private const string SelectWithDestinations = """
        SELECT r.id, r.feed_id, r.name, r.filter, r.template, r.enabled, d.sink, d.parameters
        FROM routes r LEFT JOIN destinations d ON d.route_id = r.id
        """;

    public async Task<IReadOnlyList<Route>> ListByFeedAsync(long feedId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectWithDestinations + " WHERE r.feed_id = $feedId ORDER BY r.id, d.position";
        command.Parameters.AddWithValue("$feedId", feedId);
        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<Route?> GetAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectWithDestinations + " WHERE r.id = $id ORDER BY d.position";
        command.Parameters.AddWithValue("$id", id);
        var routes = await ReadAllAsync(command, cancellationToken);
        return routes.Count == 0 ? null : routes[0];
    }

    public async Task<Route?> AddAsync(long feedId, RouteSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = await InsertAsync(connection, transaction, feedId, settings, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new Route(id, feedId, settings);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteErrors.ConstraintForeignKey)
        {
            return null;
        }
    }

    public async Task<bool> UpdateAsync(long id, RouteSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE routes SET name = $name, filter = $filter, template = $template, enabled = $enabled
                WHERE id = $id
                """;
            command.Parameters.AddWithValue("$id", id);
            AddSettingsParameters(command, settings);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return false;
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM destinations WHERE route_id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertDestinationsAsync(connection, transaction, id, settings.Destinations, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM routes WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    internal static async Task<long> InsertAsync(
        SqliteConnection connection, SqliteTransaction transaction, long feedId, RouteSettings settings, CancellationToken cancellationToken)
    {
        long id;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO routes (feed_id, name, filter, template, enabled)
                VALUES ($feedId, $name, $filter, $template, $enabled)
                RETURNING id
                """;
            command.Parameters.AddWithValue("$feedId", feedId);
            AddSettingsParameters(command, settings);
            id = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        await InsertDestinationsAsync(connection, transaction, id, settings.Destinations, cancellationToken);
        return id;
    }

    private static async Task InsertDestinationsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long routeId,
        IReadOnlyList<Destination> destinations,
        CancellationToken cancellationToken)
    {
        if (destinations.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO destinations (route_id, position, sink, parameters)
            VALUES ($routeId, $position, $sink, $parameters)
            """;
        command.Parameters.AddWithValue("$routeId", routeId);
        var position = command.Parameters.Add("$position", SqliteType.Integer);
        var sink = command.Parameters.Add("$sink", SqliteType.Text);
        var parameters = command.Parameters.Add("$parameters", SqliteType.Text);

        for (var i = 0; i < destinations.Count; i++)
        {
            position.Value = i;
            sink.Value = destinations[i].Sink;
            parameters.Value = SerializeParameters(destinations[i].Parameters);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddSettingsParameters(SqliteCommand command, RouteSettings settings)
    {
        command.Parameters.AddWithValue("$name", settings.Name);
        command.Parameters.AddWithValue("$filter", SqliteValues.OrDbNull(settings.FilterJson));
        command.Parameters.AddWithValue("$template", SqliteValues.OrDbNull(settings.TemplateJson));
        command.Parameters.AddWithValue("$enabled", settings.Enabled);
    }

    private static async Task<IReadOnlyList<Route>> ReadAllAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var routes = new List<Route>();
        var destinations = new List<Destination>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(0);
            if (routes.Count == 0 || routes[^1].Id != id)
            {
                destinations = [];
                routes.Add(new Route(
                    id,
                    reader.GetInt64(1),
                    new RouteSettings(
                        reader.GetString(2),
                        SqliteValues.GetString(reader, 3),
                        SqliteValues.GetString(reader, 4),
                        reader.GetBoolean(5),
                        destinations)));
            }

            if (!reader.IsDBNull(6))
            {
                destinations.Add(new Destination(reader.GetString(6), DeserializeParameters(reader.GetString(7))));
            }
        }

        return routes;
    }

    private static string SerializeParameters(IReadOnlyDictionary<string, string> parameters) =>
        JsonSerializer.Serialize(
            parameters as Dictionary<string, string> ?? new Dictionary<string, string>(parameters),
            ColumnJsonContext.Default.DictionaryStringString);

    private static Dictionary<string, string> DeserializeParameters(string json) =>
        JsonSerializer.Deserialize(json, ColumnJsonContext.Default.DictionaryStringString) ?? [];
}
