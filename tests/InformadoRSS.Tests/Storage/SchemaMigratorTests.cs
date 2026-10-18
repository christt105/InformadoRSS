using InformadoRSS.Storage;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Tests.Storage;

public sealed class SchemaMigratorTests : IAsyncLifetime
{
    private readonly InMemoryDatabase _db = new();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task MigrateSetsLatestVersionAndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;

        await SchemaMigrator.MigrateAsync(_db.Database, ct);

        await using var connection = await _db.Database.OpenAsync(ct);
        Assert.Equal(SchemaMigrator.LatestVersion, await SchemaMigrator.GetVersionAsync(connection, ct));
    }

    [Fact]
    public async Task MigrateRejectsNewerSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var connection = await _db.Database.OpenAsync(ct))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version = 999";
            await command.ExecuteNonQueryAsync(ct);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => SchemaMigrator.MigrateAsync(_db.Database, ct));
    }

    [Fact]
    public async Task ConnectionsEnforceForeignKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await _db.Database.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";

        Assert.Equal(1L, await command.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task FileDatabaseUsesWalAndCreatesDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("informadorss-migrator-").FullName;
        try
        {
            var database = SqliteDatabase.FromPath(Path.Combine(root, "nested", "informadorss.db"));

            await SchemaMigrator.MigrateAsync(database, ct);

            await using var connection = await database.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode";
            Assert.Equal("wal", await command.ExecuteScalarAsync(ct));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
