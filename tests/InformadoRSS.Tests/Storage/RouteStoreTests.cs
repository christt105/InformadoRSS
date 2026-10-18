using InformadoRSS.Core;
using InformadoRSS.Storage;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Tests.Storage;

public sealed class RouteStoreTests : IAsyncLifetime
{
    private static readonly Destination Panas = new("discord", new Dictionary<string, string>
    {
        ["guildId"] = "707899126955769861",
        ["channelId"] = "1",
    });

    private static readonly Destination Ntfy = new("ntfy", new Dictionary<string, string> { ["topic"] = "servidor" });

    private readonly InMemoryDatabase _db = new();
    private long _feedId;

    public async ValueTask InitializeAsync()
    {
        await _db.InitializeAsync();
        var feed = await _db.Feeds.AddAsync(
            new FeedSettings(new Uri("https://example.com/feed.xml"), "Feed", TimeSpan.FromMinutes(10)),
            TestContext.Current.CancellationToken);
        _feedId = feed.Id;
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task AddedRouteKeepsDestinationsInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new RouteSettings("Movies", """{"contains":"movie"}""", """{"content":"{{title}}"}""", true, [Panas, Ntfy]);

        var added = await _db.Routes.AddAsync(_feedId, settings, ct);

        var route = await _db.Routes.GetAsync(added!.Id, ct);
        Assert.NotNull(route);
        Assert.Equal(_feedId, route.FeedId);
        AssertSettingsEqual(settings, route.Settings);
    }

    [Fact]
    public async Task ListByFeedReturnsOnlyThatFeedsRoutes()
    {
        var ct = TestContext.Current.CancellationToken;
        var other = await _db.Feeds.AddAsync(
            new FeedSettings(new Uri("https://example.com/other.xml"), "Other", TimeSpan.FromMinutes(5)), ct);
        var first = await _db.Routes.AddAsync(_feedId, new RouteSettings("A", null, null, true, [Panas]), ct);
        var second = await _db.Routes.AddAsync(_feedId, new RouteSettings("B", null, null, false, []), ct);
        await _db.Routes.AddAsync(other.Id, new RouteSettings("C", null, null, true, [Ntfy]), ct);

        var routes = await _db.Routes.ListByFeedAsync(_feedId, ct);

        Assert.Equal([first!.Id, second!.Id], routes.Select(r => r.Id));
        Assert.Single(routes[0].Settings.Destinations);
        Assert.Empty(routes[1].Settings.Destinations);
        Assert.False(routes[1].Settings.Enabled);
    }

    [Fact]
    public async Task AddToMissingFeedReturnsNull()
    {
        Assert.Null(await _db.Routes.AddAsync(
            _feedId + 100, new RouteSettings("A", null, null, true, [Panas]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateReplacesDestinationsAndKeepsSeenItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var route = await _db.Routes.AddAsync(_feedId, new RouteSettings("A", null, null, true, [Panas, Ntfy]), ct);
        await _db.SeenItems.MarkSeenAsync(route!.Id, ["item"], DateTimeOffset.UnixEpoch, ct);
        var changed = new RouteSettings("B", """{"not":true}""", null, false, [Ntfy]);

        Assert.True(await _db.Routes.UpdateAsync(route.Id, changed, ct));
        Assert.False(await _db.Routes.UpdateAsync(route.Id + 1, changed, ct));

        AssertSettingsEqual(changed, (await _db.Routes.GetAsync(route.Id, ct))!.Settings);
        Assert.Empty(await _db.SeenItems.FilterUnseenAsync(route.Id, ["item"], ct));
    }

    [Fact]
    public async Task DeleteRemovesRoute()
    {
        var ct = TestContext.Current.CancellationToken;
        var route = await _db.Routes.AddAsync(_feedId, new RouteSettings("A", null, null, true, [Panas]), ct);

        Assert.True(await _db.Routes.DeleteAsync(route!.Id, ct));
        Assert.False(await _db.Routes.DeleteAsync(route.Id, ct));
        Assert.Empty(await _db.Routes.ListByFeedAsync(_feedId, ct));
    }

    [Fact]
    public async Task BrokenDestinationsAreListedUntilClearedOrReplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await _db.Routes.AddAsync(_feedId, new RouteSettings("A", null, null, true, [Ntfy, Panas]), ct);
        var second = await _db.Routes.AddAsync(_feedId, new RouteSettings("B", null, null, true, [Panas]), ct);
        var since = new DateTimeOffset(2026, 10, 18, 4, 0, 0, TimeSpan.Zero);

        Assert.Empty(await _db.Routes.ListBrokenDestinationsAsync(ct));

        await _db.Routes.SetDestinationBrokenAsync(first!.Id, 1, since, "Unknown Channel", ct);
        await _db.Routes.SetDestinationBrokenAsync(first.Id, 1, since.AddHours(1), "Missing Access", ct);
        await _db.Routes.SetDestinationBrokenAsync(second!.Id, 0, since, "Missing Permissions", ct);

        var broken = await _db.Routes.ListBrokenDestinationsAsync(ct);
        Assert.Equal(
            [(first.Id, _feedId, 1, since, "Missing Access"), (second.Id, _feedId, 0, since, "Missing Permissions")],
            broken.Select(b => (b.RouteId, b.FeedId, b.Position, b.Since, b.Reason)));
        Assert.Equal("discord", broken[0].Destination.Sink);
        Assert.Equal(Panas.Parameters, broken[0].Destination.Parameters);

        await _db.Routes.SetDestinationBrokenAsync(first.Id, 1, since, null, ct);
        await _db.Routes.UpdateAsync(second.Id, second.Settings, ct);

        Assert.Empty(await _db.Routes.ListBrokenDestinationsAsync(ct));
    }

    [Fact]
    public async Task InvalidJsonIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SqliteException>(() =>
            _db.Routes.AddAsync(_feedId, new RouteSettings("A", "{not json", null, true, []), ct));
        Assert.Empty(await _db.Routes.ListByFeedAsync(_feedId, ct));
    }

    private static void AssertSettingsEqual(RouteSettings expected, RouteSettings actual)
    {
        Assert.Equal(expected with { Destinations = Array.Empty<Destination>() }, actual with { Destinations = Array.Empty<Destination>() });
        Assert.Equal(expected.Destinations.Count, actual.Destinations.Count);
        for (var i = 0; i < expected.Destinations.Count; i++)
        {
            Assert.Equal(expected.Destinations[i].Sink, actual.Destinations[i].Sink);
            Assert.Equal(expected.Destinations[i].Parameters, actual.Destinations[i].Parameters);
        }
    }
}
