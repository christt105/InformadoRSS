using InformadoRSS.Storage;

namespace InformadoRSS.Tests.Storage;

public sealed class LatestItemStoreTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDatabase _db = new();

    public ValueTask InitializeAsync() => _db.InitializeAsync();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task SameItemKeepsItsFirstFetchTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var feedId = await AddFeedAsync();
        var item = new LatestItem("a", "Hello", "https://example.com/a", T0, T0);

        await _db.LatestItems.SetAsync(feedId, item, ct);
        await _db.LatestItems.SetAsync(feedId, item with { FetchedAt = T0.AddMinutes(10) }, ct);

        Assert.Equal(item, (await _db.LatestItems.ListAsync(ct))[feedId]);
    }

    [Fact]
    public async Task ChangedItemReplacesThePreviousOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var feedId = await AddFeedAsync();
        await _db.LatestItems.SetAsync(feedId, new LatestItem("a", "Hello", "https://example.com/a", T0, T0), ct);

        var retitled = new LatestItem("a", "Hello again", "https://example.com/a", T0, T0.AddMinutes(10));
        await _db.LatestItems.SetAsync(feedId, retitled, ct);
        Assert.Equal(retitled, (await _db.LatestItems.ListAsync(ct))[feedId]);

        var next = new LatestItem("b", "Next", "https://example.com/b", T0.AddMinutes(15), T0.AddMinutes(20));
        await _db.LatestItems.SetAsync(feedId, next, ct);
        Assert.Equal(next, (await _db.LatestItems.ListAsync(ct))[feedId]);
    }

    private async Task<long> AddFeedAsync()
    {
        var settings = new FeedSettings(new Uri("https://example.com/blog.xml"), "Blog", TimeSpan.FromMinutes(10));
        return (await _db.Feeds.AddAsync(settings, TestContext.Current.CancellationToken)).Id;
    }
}
