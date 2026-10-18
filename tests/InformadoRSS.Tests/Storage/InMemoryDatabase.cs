using InformadoRSS.Storage;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Tests.Storage;

/// <summary>A migrated shared-cache in-memory database that lives as long as this object.</summary>
public sealed class InMemoryDatabase : IAsyncLifetime
{
    private readonly SqliteConnection _keepAlive;

    public InMemoryDatabase()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"informadorss-test-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        _keepAlive = new SqliteConnection(connectionString);
        Database = new SqliteDatabase(connectionString);
        Feeds = new SqliteFeedStore(Database);
        Routes = new SqliteRouteStore(Database);
        SeenItems = new SqliteSeenItemStore(Database);
        Settings = new SqliteSettingsStore(Database);
        Transfer = new ConfigurationTransfer(Database, Feeds, Routes, Settings);
    }

    public SqliteDatabase Database { get; }

    public SqliteFeedStore Feeds { get; }

    public SqliteRouteStore Routes { get; }

    public SqliteSeenItemStore SeenItems { get; }

    public SqliteSettingsStore Settings { get; }

    public ConfigurationTransfer Transfer { get; }

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync();
        await SchemaMigrator.MigrateAsync(Database);
    }

    public ValueTask DisposeAsync() => _keepAlive.DisposeAsync();
}
