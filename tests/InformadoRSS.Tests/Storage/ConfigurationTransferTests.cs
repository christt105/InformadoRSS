using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InformadoRSS.Core;
using InformadoRSS.Storage;

namespace InformadoRSS.Tests.Storage;

public sealed class ConfigurationTransferTests : IAsyncLifetime
{
    private readonly InMemoryDatabase _source = new();
    private readonly InMemoryDatabase _target = new();

    public async ValueTask InitializeAsync()
    {
        await _source.InitializeAsync();
        await _target.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _source.DisposeAsync();
        await _target.DisposeAsync();
    }

    [Fact]
    public async Task ExportEmbedsFilterAndTemplateAsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(_source, ct);

        var json = await ExportJsonAsync(_source, ct);

        var root = JsonNode.Parse(json)!;
        Assert.Equal(ConfigurationSnapshot.CurrentVersion, root["version"]!.GetValue<int>());
        var feeds = root["feeds"]!.AsArray();
        Assert.Equal(2, feeds.Count);
        var movies = feeds.Single(f => f!["name"]!.GetValue<string>() == "Media Tracker")!["routes"]![0]!;
        Assert.Equal("movie", movies["filter"]!["value"]!.GetValue<string>());
        Assert.Equal("1", movies["destinations"]![0]!["parameters"]!["channelId"]!.GetValue<string>());
        Assert.Equal("^[^-]+", feeds.Single(f => f!["name"]!.GetValue<string>() == "List")!["identityPattern"]!.GetValue<string>());
        Assert.Equal("t0k3n", root["settings"]!["discord.token"]!.GetValue<string>());
        Assert.DoesNotContain("etag", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRestoresExportedConfiguration()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(_source, ct);
        await _target.Feeds.AddAsync(new FeedSettings(new Uri("https://example.com/stale.xml"), "Stale", TimeSpan.FromHours(1)), ct);
        await _target.Settings.SetAsync("stale", "yes", ct);
        var json = await ExportJsonAsync(_source, ct);

        using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            await _target.Transfer.ImportAsync(input, ct);
        }

        Assert.Equal(json, await ExportJsonAsync(_target, ct));
        Assert.Null(await _target.Settings.GetAsync("stale", ct));
    }

    [Fact]
    public async Task FailedImportLeavesConfigurationUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(_target, ct);
        var before = await ExportJsonAsync(_target, ct);
        var feed = new FeedSnapshot(new Uri("https://example.com/dup.xml"), "Dup", 600, []);

        await Assert.ThrowsAsync<DuplicateFeedException>(() => _target.Transfer.ImportAsync(
            new ConfigurationSnapshot(ConfigurationSnapshot.CurrentVersion, [feed, feed], new Dictionary<string, string>()), ct));

        Assert.Equal(before, await ExportJsonAsync(_target, ct));
    }

    [Fact]
    public async Task ImportRejectsNewerVersion()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _target.Transfer.ImportAsync(
            new ConfigurationSnapshot(ConfigurationSnapshot.CurrentVersion + 1, [], new Dictionary<string, string>()),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("""{"version":1,"settings":{}}""")]
    [InlineData("""{"version":1,"feeds":[{"url":"https://example.com/a.xml","name":"A","intervalSeconds":600}],"settings":{}}""")]
    [InlineData("""{"version":1,"feeds":null,"settings":{}}""")]
    public async Task ImportRejectsIncompleteDocuments(string json)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<JsonException>(() => _target.Transfer.ImportAsync(input, TestContext.Current.CancellationToken));
    }

    private static async Task SeedAsync(InMemoryDatabase db, CancellationToken ct)
    {
        var tracker = await db.Feeds.AddAsync(
            new FeedSettings(new Uri("https://christt105.github.io/MediaTracker/finished.xml"), "Media Tracker", TimeSpan.FromMinutes(10)), ct);
        await db.Feeds.RecordSuccessAsync(tracker.Id, DateTimeOffset.UnixEpoch, new FeedValidators("\"etag\"", null), ct);
        var movies = await db.Routes.AddAsync(
            tracker.Id,
            new RouteSettings(
                "Movies",
                """{"field":"category:type","op":"equals","value":"movie"}""",
                """{"content":"**{{title}}**\n{{link}}"}""",
                true,
                [
                    new Destination("discord", new Dictionary<string, string> { ["guildId"] = "707899126955769861", ["channelId"] = "1" }),
                    new Destination("ntfy", new Dictionary<string, string> { ["topic"] = "servidor" }),
                ]),
            ct);
        await db.SeenItems.MarkSeenAsync(movies!.Id, ["seen"], DateTimeOffset.UnixEpoch, ct);
        await db.Routes.AddAsync(tracker.Id, new RouteSettings("Games", null, null, false, []), ct);
        await db.Feeds.AddAsync(new FeedSettings(new Uri("https://example.com/list.xml"), "List", TimeSpan.FromHours(1), "^[^-]+"), ct);
        await db.Settings.SetAsync("discord.token", "t0k3n", ct);
    }

    private static async Task<string> ExportJsonAsync(InMemoryDatabase db, CancellationToken ct)
    {
        using var output = new MemoryStream();
        await db.Transfer.ExportAsync(output, ct);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
