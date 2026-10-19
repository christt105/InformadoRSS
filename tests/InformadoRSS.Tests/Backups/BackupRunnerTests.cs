using System.Net;
using System.Text.Json;
using InformadoRSS.Alerts;
using InformadoRSS.Backups;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;
using InformadoRSS.Tests.Scheduling;
using InformadoRSS.Tests.Sinks.Discord;
using InformadoRSS.Tests.Sinks.Ntfy;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace InformadoRSS.Tests.Backups;

public sealed class BackupRunnerTests : IAsyncLifetime
{
    private const string AlertChannel = "4242";
    private const string AlertRoute = $"POST channels/{AlertChannel}/messages";

    private static readonly DateTimeOffset Start = new(2026, 10, 19, 3, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Start);
    private readonly FakeDiscordApi _discord = new();
    private readonly FakeNtfyServer _ntfy = new();
    private readonly string _root = Directory.CreateTempSubdirectory("informadorss-backups-").FullName;
    private SqliteDatabase _database = null!;
    private SqliteSettingsStore _settings = null!;
    private string _directory = null!;
    private BackupRunner _runner = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _database = SqliteDatabase.FromPath(Path.Combine(_root, "informadorss.db"));
        await SchemaMigrator.MigrateAsync(_database, ct);
        _settings = new SqliteSettingsStore(_database);
        _directory = Path.Combine(_root, "backups");
        await _settings.SetAsync(BackupSettings.DirectoryKey, _directory, ct);
        await _settings.SetAsync(AlertSettings.DiscordChannelIdKey, AlertChannel, ct);
        await new SqliteFeedStore(_database).AddAsync(new FeedSettings(new Uri("https://example.com/feed.xml"), "Blog", TimeSpan.FromMinutes(10)), ct);
        _discord.On(AlertRoute, """{"id":"1"}""");

        var discord = new DiscordRestClient(
            new HttpClient(_discord), new MutableTokenProvider("token"), new DiscordRateLimiter(_time), new DiscordClientOptions(), _time);
        var sender = new AlertSender(discord, new NtfyClient(new HttpClient(_ntfy), _settings), NullLogger<AlertSender>.Instance);
        _runner = new BackupRunner(_database, _settings, sender, _time, NullLogger<BackupRunner>.Instance);
    }

    public ValueTask DisposeAsync()
    {
        _discord.Dispose();
        _ntfy.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private Task<BackupResult> RunAsync() => _runner.RunIfDueAsync(TestContext.Current.CancellationToken);

    private List<string> Backups() => [.. BackupFiles.List(_directory).Select(file => Path.GetFileName(file.Path))];

    private List<string> AlertTitles() =>
        [.. _discord.Requests
            .Zip(_discord.RequestedRoutes)
            .Where(pair => pair.Second == AlertRoute)
            .Select(pair => JsonDocument.Parse(pair.First.Body).RootElement.GetProperty("embeds")[0].GetProperty("title").GetString()!)];

    [Fact]
    public async Task WithoutFolderNothingIsWritten()
    {
        await _settings.SetAsync(BackupSettings.DirectoryKey, null, TestContext.Current.CancellationToken);

        Assert.Equal(BackupResult.Disabled, await RunAsync());
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task WritesACompleteCopyOfTheDatabase()
    {
        Assert.Equal(BackupResult.Created, await RunAsync());

        var path = Assert.Single(BackupFiles.List(_directory)).Path;
        Assert.Equal("informadorss-20261019T030000Z.db", Path.GetFileName(path));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));

        await using var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await copy.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = copy.CreateCommand();
        command.CommandText = "SELECT name FROM feeds";
        Assert.Equal("Blog", await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitsForTheIntervalSinceTheNewestBackup()
    {
        await RunAsync();
        _time.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));
        Assert.Equal(BackupResult.NotDue, await RunAsync());

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(BackupResult.Created, await RunAsync());
        Assert.Equal(2, Backups().Count);
    }

    [Fact]
    public async Task KeepsOnlyTheConfiguredNumberOfBackups()
    {
        await _settings.SetAsync(BackupSettings.KeepKey, "2", TestContext.Current.CancellationToken);
        for (var day = 0; day < 4; day++)
        {
            await RunAsync();
            _time.Advance(TimeSpan.FromDays(1));
        }

        Assert.Equal(["informadorss-20261022T030000Z.db", "informadorss-20261021T030000Z.db"], Backups());
    }

    [Fact]
    public async Task FailureAlertsOnceAndRecoveryOnce()
    {
        await File.WriteAllTextAsync(_directory, "a file where the folder should be", TestContext.Current.CancellationToken);

        Assert.Equal(BackupResult.Failed, await RunAsync());
        _time.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(BackupResult.Failed, await RunAsync());
        Assert.Equal(["Backup failing"], AlertTitles());

        File.Delete(_directory);
        _time.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(BackupResult.Created, await RunAsync());
        _time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(BackupResult.Created, await RunAsync());

        Assert.Equal(["Backup failing", "Backup working again"], AlertTitles());
    }

    [Fact]
    public async Task TemporarilyUndeliveredFailureAlertIsRetried()
    {
        await File.WriteAllTextAsync(_directory, "a file where the folder should be", TestContext.Current.CancellationToken);
        _discord.On(AlertRoute, () => FakeDiscordApi.Json(HttpStatusCode.BadGateway, "{}"));
        await RunAsync();

        _discord.On(AlertRoute, """{"id":"1"}""");
        _time.Advance(TimeSpan.FromMinutes(15));
        await RunAsync();
        _time.Advance(TimeSpan.FromMinutes(15));
        await RunAsync();

        Assert.Equal(2, AlertTitles().Count);
    }
}
