using InformadoRSS.Core;
using InformadoRSS.Storage;

namespace InformadoRSS.Tests.Storage;

public sealed class AlertStateStoreTests : IAsyncLifetime
{
    private static readonly DateTimeOffset At = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDatabase _db = new();
    private SqliteAlertStateStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        await _db.InitializeAsync();
        _store = new SqliteAlertStateStore(_db.Database);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private Task<Feed> AddFeedAsync(string name) =>
        _db.Feeds.AddAsync(
            new FeedSettings(new Uri($"https://example.com/{name}.xml"), name, TimeSpan.FromMinutes(10)),
            TestContext.Current.CancellationToken);

    private static Destination Channel(string id) => new("discord", new Dictionary<string, string> { ["channelId"] = id });

    [Fact]
    public async Task FeedStateRoundTripsAndIsDroppedWithTheFeed()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await AddFeedAsync("a");

        await _store.SetFeedStateAsync(feed.Id, new FeedAlertState(At, null), ct);
        Assert.Equal(new FeedAlertState(At, null), await _store.GetFeedStateAsync(feed.Id, ct));

        await _store.SetFeedStateAsync(feed.Id, new FeedAlertState(At, At.AddHours(6)), ct);
        Assert.Equal(new FeedAlertState(At, At.AddHours(6)), await _store.GetFeedStateAsync(feed.Id, ct));

        await _db.Feeds.DeleteAsync(feed.Id, ct);
        Assert.Null(await _store.GetFeedStateAsync(feed.Id, ct));

        await _store.SetFeedStateAsync(feed.Id, new FeedAlertState(At, null), ct);
        Assert.Null(await _store.GetFeedStateAsync(feed.Id, ct));
    }

    [Fact]
    public async Task ClearRemovesFeedState()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await AddFeedAsync("a");
        await _store.SetFeedStateAsync(feed.Id, new FeedAlertState(At, At), ct);

        await _store.ClearFeedStateAsync(feed.Id, ct);

        Assert.Null(await _store.GetFeedStateAsync(feed.Id, ct));
    }

    [Fact]
    public async Task DestinationStatesCoverBrokenOrAlertedDestinationsOfTheFeedOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await AddFeedAsync("a");
        var other = await AddFeedAsync("b");
        var route = (await _db.Routes.AddAsync(feed.Id, new RouteSettings("R", null, null, true, [Channel("1"), Channel("2")]), ct))!;
        var otherRoute = (await _db.Routes.AddAsync(other.Id, new RouteSettings("O", null, null, true, [Channel("3")]), ct))!;
        await _db.Routes.SetDestinationBrokenAsync(route.Id, 1, At, "gone", ct);
        await _db.Routes.SetDestinationBrokenAsync(otherRoute.Id, 0, At, "gone", ct);

        var state = Assert.Single(await _store.ListDestinationStatesAsync(feed.Id, ct));
        Assert.Equal(route.Id, state.RouteId);
        Assert.Equal("R", state.RouteName);
        Assert.Equal(1, state.Position);
        Assert.Equal("2", state.Destination.Parameters["channelId"]);
        Assert.Equal(At, state.BrokenSince);
        Assert.Equal("gone", state.BrokenReason);
        Assert.Null(state.AlertedAt);

        await _store.SetDestinationAlertedAsync(state.DestinationId, At.AddMinutes(1), ct);
        await _db.Routes.SetDestinationBrokenAsync(route.Id, 1, At, null, ct);

        state = Assert.Single(await _store.ListDestinationStatesAsync(feed.Id, ct));
        Assert.Null(state.BrokenSince);
        Assert.Equal(At.AddMinutes(1), state.AlertedAt);

        await _store.SetDestinationAlertedAsync(state.DestinationId, null, ct);
        Assert.Empty(await _store.ListDestinationStatesAsync(feed.Id, ct));
    }

    [Fact]
    public async Task DestinationAlertIsDroppedWhenDestinationsAreReplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await AddFeedAsync("a");
        var route = (await _db.Routes.AddAsync(feed.Id, new RouteSettings("R", null, null, true, [Channel("1")]), ct))!;
        await _db.Routes.SetDestinationBrokenAsync(route.Id, 0, At, "gone", ct);
        var state = Assert.Single(await _store.ListDestinationStatesAsync(feed.Id, ct));
        await _store.SetDestinationAlertedAsync(state.DestinationId, At, ct);

        await _db.Routes.UpdateAsync(route.Id, route.Settings, ct);

        Assert.Empty(await _store.ListDestinationStatesAsync(feed.Id, ct));
    }

    [Fact]
    public async Task DestinationAlertForMissingDestinationIsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await AddFeedAsync("a");

        await _store.SetDestinationAlertedAsync(12345, At, ct);

        Assert.Empty(await _store.ListDestinationStatesAsync(feed.Id, ct));
    }
}
