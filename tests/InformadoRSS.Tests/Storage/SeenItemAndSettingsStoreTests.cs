using InformadoRSS.Storage;

namespace InformadoRSS.Tests.Storage;

public sealed class SeenItemAndSettingsStoreTests : IAsyncLifetime
{
    private readonly InMemoryDatabase _db = new();
    private long _routeA;
    private long _routeB;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.InitializeAsync();
        var feed = await _db.Feeds.AddAsync(
            new FeedSettings(new Uri("https://example.com/feed.xml"), "Feed", TimeSpan.FromMinutes(10)), ct);
        _routeA = (await _db.Routes.AddAsync(feed.Id, new RouteSettings("A", null, null, true, []), ct))!.Id;
        _routeB = (await _db.Routes.AddAsync(feed.Id, new RouteSettings("B", null, null, true, []), ct))!.Id;
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task SeenItemsAreTrackedPerRoute()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.False(await _db.SeenItems.HasAnyAsync(_routeA, ct));

        Assert.Equal(2, await _db.SeenItems.MarkSeenAsync(_routeA, ["1", "2"], DateTimeOffset.UnixEpoch, ct));
        Assert.Equal(1, await _db.SeenItems.MarkSeenAsync(_routeA, ["2", "3", "3"], DateTimeOffset.UnixEpoch, ct));

        Assert.True(await _db.SeenItems.HasAnyAsync(_routeA, ct));
        Assert.False(await _db.SeenItems.HasAnyAsync(_routeB, ct));
        Assert.Equal(["4", "0"], await _db.SeenItems.FilterUnseenAsync(_routeA, ["4", "1", "0", "3"], ct));
        Assert.Equal(["1", "2"], await _db.SeenItems.FilterUnseenAsync(_routeB, ["1", "2"], ct));
        Assert.Empty(await _db.SeenItems.FilterUnseenAsync(_routeA, [], ct));
    }

    [Fact]
    public async Task SettingsCanBeSetOverwrittenAndRemoved()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.Null(await _db.Settings.GetAsync("discord.token", ct));

        await _db.Settings.SetAsync("discord.token", "one", ct);
        await _db.Settings.SetAsync("discord.token", "two", ct);
        await _db.Settings.SetAsync("alerts.channel", "123", ct);

        Assert.Equal("two", await _db.Settings.GetAsync("discord.token", ct));
        Assert.Equal(
            new Dictionary<string, string> { ["alerts.channel"] = "123", ["discord.token"] = "two" },
            await _db.Settings.ListAsync(ct));

        await _db.Settings.SetAsync("discord.token", null, ct);

        Assert.Null(await _db.Settings.GetAsync("discord.token", ct));
        Assert.Single(await _db.Settings.ListAsync(ct));
    }
}
