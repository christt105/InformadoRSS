using InformadoRSS.Storage;

namespace InformadoRSS.Tests.Storage;

public sealed class FeedStoreTests : IAsyncLifetime
{
    private static readonly FeedSettings Blog = new(new Uri("https://example.com/blog.xml"), "Blog", TimeSpan.FromMinutes(10));

    private readonly InMemoryDatabase _db = new();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task AddedFeedCanBeReadBack()
    {
        var ct = TestContext.Current.CancellationToken;

        var added = await _db.Feeds.AddAsync(Blog, ct);

        var feed = await _db.Feeds.GetAsync(added.Id, ct);
        Assert.NotNull(feed);
        Assert.Equal(Blog, feed.Settings);
        Assert.Equal(FeedHealth.Unknown, feed.Health);
        Assert.Equal(FeedValidators.None, feed.Validators);
        Assert.Equal([added.Id], (await _db.Feeds.ListAsync(ct)).Select(f => f.Id));
    }

    [Fact]
    public async Task GetReturnsNullForMissingFeed()
    {
        Assert.Null(await _db.Feeds.GetAsync(42, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicateUrlIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.Feeds.AddAsync(Blog, ct);
        var other = await _db.Feeds.AddAsync(Blog with { Url = new Uri("https://example.com/other.xml") }, ct);

        await Assert.ThrowsAsync<DuplicateFeedException>(() => _db.Feeds.AddAsync(Blog with { Name = "Copy" }, ct));
        await Assert.ThrowsAsync<DuplicateFeedException>(() => _db.Feeds.UpdateAsync(other.Id, Blog, ct));
    }

    [Fact]
    public async Task UpdateChangesSettingsAndReportsMissingFeed()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        var changed = Blog with { Name = "Renamed", Interval = TimeSpan.FromMinutes(30) };

        Assert.True(await _db.Feeds.UpdateAsync(feed.Id, changed, ct));
        Assert.False(await _db.Feeds.UpdateAsync(feed.Id + 1, changed, ct));
        Assert.Equal(changed, (await _db.Feeds.GetAsync(feed.Id, ct))!.Settings);
    }

    [Fact]
    public async Task HealthTracksSuccessesAndFailures()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        var t0 = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var validators = new FeedValidators("\"abc\"", "Sat, 17 Oct 2026 10:00:00 GMT");

        await _db.Feeds.RecordSuccessAsync(feed.Id, t0, validators, ct);
        await _db.Feeds.RecordFailureAsync(feed.Id, t0.AddMinutes(10), "timeout", ct);
        await _db.Feeds.RecordFailureAsync(feed.Id, t0.AddMinutes(20), "HTTP 500", ct);

        var failing = (await _db.Feeds.GetAsync(feed.Id, ct))!;
        Assert.Equal(new FeedHealth(2, t0, t0.AddMinutes(20), "HTTP 500"), failing.Health);
        Assert.Equal(validators, failing.Validators);

        await _db.Feeds.RecordSuccessAsync(feed.Id, t0.AddMinutes(30), FeedValidators.None, ct);

        var recovered = (await _db.Feeds.GetAsync(feed.Id, ct))!;
        Assert.Equal(0, recovered.Health.ConsecutiveFailures);
        Assert.Equal(t0.AddMinutes(30), recovered.Health.LastSuccessAt);
        Assert.Equal(FeedValidators.None, recovered.Validators);
    }

    [Fact]
    public async Task UnchangedSuccessRefreshesItsTimeOnlyOncePerInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        var t0 = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var validators = new FeedValidators("\"abc\"", null);

        await _db.Feeds.RecordSuccessAsync(feed.Id, t0, validators, ct);
        await _db.Feeds.RecordSuccessAsync(feed.Id, t0.AddMinutes(10), validators, ct);
        Assert.Equal(t0, (await _db.Feeds.GetAsync(feed.Id, ct))!.Health.LastSuccessAt);

        await _db.Feeds.RecordSuccessAsync(feed.Id, t0 + SqliteFeedStore.SuccessRefreshInterval, validators, ct);
        Assert.Equal(t0 + SqliteFeedStore.SuccessRefreshInterval, (await _db.Feeds.GetAsync(feed.Id, ct))!.Health.LastSuccessAt);
    }

    [Fact]
    public async Task SuccessIsWrittenAtOnceWhenValidatorsOrFailuresChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        var t0 = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var changed = new FeedValidators("\"def\"", null);

        await _db.Feeds.RecordSuccessAsync(feed.Id, t0, new FeedValidators("\"abc\"", null), ct);
        await _db.Feeds.RecordSuccessAsync(feed.Id, t0.AddMinutes(10), changed, ct);

        var revalidated = (await _db.Feeds.GetAsync(feed.Id, ct))!;
        Assert.Equal(t0.AddMinutes(10), revalidated.Health.LastSuccessAt);
        Assert.Equal(changed, revalidated.Validators);

        await _db.Feeds.RecordFailureAsync(feed.Id, t0.AddMinutes(20), "HTTP 404", ct);
        await _db.Feeds.RecordSuccessAsync(feed.Id, t0.AddMinutes(30), changed, ct);

        var recovered = (await _db.Feeds.GetAsync(feed.Id, ct))!;
        Assert.Equal(0, recovered.Health.ConsecutiveFailures);
        Assert.Equal(t0.AddMinutes(30), recovered.Health.LastSuccessAt);
    }

    [Fact]
    public async Task ChangingUrlDropsValidators()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        await _db.Feeds.RecordSuccessAsync(feed.Id, DateTimeOffset.UnixEpoch, new FeedValidators("\"abc\"", null), ct);

        await _db.Feeds.UpdateAsync(feed.Id, Blog with { Name = "Renamed" }, ct);
        Assert.Equal("\"abc\"", (await _db.Feeds.GetAsync(feed.Id, ct))!.Validators.ETag);

        await _db.Feeds.UpdateAsync(feed.Id, Blog with { Url = new Uri("https://example.com/new.xml") }, ct);
        Assert.Equal(FeedValidators.None, (await _db.Feeds.GetAsync(feed.Id, ct))!.Validators);
    }

    [Fact]
    public async Task DeleteCascadesToRoutesAndSeenItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        var route = await _db.Routes.AddAsync(feed.Id, new RouteSettings("All", null, null, true, []), ct);
        await _db.SeenItems.MarkSeenAsync(route!.Id, ["a"], DateTimeOffset.UnixEpoch, ct);

        Assert.True(await _db.Feeds.DeleteAsync(feed.Id, ct));
        Assert.False(await _db.Feeds.DeleteAsync(feed.Id, ct));

        Assert.Null(await _db.Routes.GetAsync(route.Id, ct));
        Assert.Equal(["a"], await _db.SeenItems.FilterUnseenAsync(route.Id, ["a"], ct));
    }

    [Fact]
    public async Task IdentityPatternIsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = Blog with { IdentityPattern = "^[^-]+" };

        var feed = await _db.Feeds.AddAsync(settings, ct);
        Assert.Equal(settings, (await _db.Feeds.GetAsync(feed.Id, ct))!.Settings);

        await _db.Feeds.UpdateAsync(feed.Id, Blog, ct);
        Assert.Null((await _db.Feeds.GetAsync(feed.Id, ct))!.Settings.IdentityPattern);
    }

    [Fact]
    public async Task ChangingIdentityPatternUnseedsRoutes()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _db.Feeds.AddAsync(Blog, ct);
        var other = await _db.Feeds.AddAsync(Blog with { Url = new Uri("https://example.com/other.xml") }, ct);
        var route = await _db.Routes.AddAsync(feed.Id, new RouteSettings("All", null, null, true, []), ct);
        var otherRoute = await _db.Routes.AddAsync(other.Id, new RouteSettings("All", null, null, true, []), ct);
        await _db.SeenItems.SeedAsync(route!.Id, ["a"], DateTimeOffset.UnixEpoch, ct);
        await _db.SeenItems.SeedAsync(otherRoute!.Id, ["a"], DateTimeOffset.UnixEpoch, ct);

        await _db.Feeds.UpdateAsync(feed.Id, Blog with { Name = "Renamed" }, ct);
        Assert.True(await _db.SeenItems.IsSeededAsync(route.Id, ct));

        await _db.Feeds.UpdateAsync(feed.Id, Blog with { IdentityPattern = "^[^-]+" }, ct);
        Assert.False(await _db.SeenItems.IsSeededAsync(route.Id, ct));
        Assert.True(await _db.SeenItems.IsSeededAsync(otherRoute.Id, ct));
        Assert.Empty(await _db.SeenItems.FilterUnseenAsync(route.Id, ["a"], ct));

        await _db.SeenItems.SeedAsync(route.Id, ["a#Watching"], DateTimeOffset.UnixEpoch, ct);
        await _db.Feeds.UpdateAsync(feed.Id, Blog with { IdentityPattern = "^[^-]+" }, ct);
        Assert.True(await _db.SeenItems.IsSeededAsync(route.Id, ct));
    }
}
