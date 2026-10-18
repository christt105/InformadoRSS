using System.Globalization;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

/// <summary>
/// Applies the schema scripts in order. Script <c>n</c> (1-based) upgrades a database at
/// <c>PRAGMA user_version = n - 1</c> to version <c>n</c>. Scripts are append-only: never edit a released one.
/// </summary>
public static class SchemaMigrator
{
    internal static readonly string[] Scripts =
    [
        """
        CREATE TABLE feeds (
            id INTEGER PRIMARY KEY,
            url TEXT NOT NULL UNIQUE,
            name TEXT NOT NULL,
            interval_seconds INTEGER NOT NULL CHECK (interval_seconds > 0),
            consecutive_failures INTEGER NOT NULL DEFAULT 0,
            last_success_at INTEGER,
            last_error_at INTEGER,
            last_error TEXT,
            etag TEXT,
            last_modified TEXT
        );

        CREATE TABLE routes (
            id INTEGER PRIMARY KEY,
            feed_id INTEGER NOT NULL REFERENCES feeds (id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            filter TEXT CHECK (filter IS NULL OR json_valid(filter)),
            template TEXT CHECK (template IS NULL OR json_valid(template)),
            enabled INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0, 1))
        );

        CREATE INDEX routes_feed_id ON routes (feed_id);

        CREATE TABLE destinations (
            id INTEGER PRIMARY KEY,
            route_id INTEGER NOT NULL REFERENCES routes (id) ON DELETE CASCADE,
            position INTEGER NOT NULL,
            sink TEXT NOT NULL,
            parameters TEXT NOT NULL CHECK (json_valid(parameters))
        );

        CREATE INDEX destinations_route_id ON destinations (route_id, position);

        CREATE TABLE seen_items (
            route_id INTEGER NOT NULL REFERENCES routes (id) ON DELETE CASCADE,
            item_id TEXT NOT NULL,
            seen_at INTEGER NOT NULL,
            PRIMARY KEY (route_id, item_id)
        ) WITHOUT ROWID;

        CREATE TABLE settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        ) WITHOUT ROWID;
        """,
    ];

    /// <summary>The schema version this build expects.</summary>
    public static int LatestVersion => Scripts.Length;

    /// <summary>Enables WAL for file databases and applies pending scripts, each in its own transaction.</summary>
    /// <exception cref="InvalidOperationException">The database was written by a newer version of the app.</exception>
    public static async Task MigrateAsync(SqliteDatabase database, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);

        await using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode = WAL";
            await wal.ExecuteNonQueryAsync(cancellationToken);
        }

        var current = await GetVersionAsync(connection, cancellationToken);
        if (current > LatestVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {current} is newer than the supported version {LatestVersion}.");
        }

        for (var version = current + 1; version <= LatestVersion; version++)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = Scripts[version - 1] + ";\nPRAGMA user_version = " + version.ToString(CultureInfo.InvariantCulture);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public static async Task<int> GetVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }
}
