using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using static InformadoRSS.Tests.Scheduling.PipelineHarness;

namespace InformadoRSS.Tests.Scheduling;

public sealed class FeedSchedulerTests : IAsyncLifetime
{
    private static readonly FetchResult Failure = new FeedFetchFailed(new FetchError(FetchErrorKind.Network, "connection reset"));

    private PipelineHarness _h = null!;

    public async ValueTask InitializeAsync() => _h = await CreateAsync();

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    [Fact]
    public async Task SameFeedIsNeverFetchedTwiceAtOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var gate = new TaskCompletionSource();
        _h.Source.Handler = async (_, token) =>
        {
            await gate.Task.WaitAsync(token);
            return new FeedFetched(null, [], CacheValidators.None);
        };
        using var scheduler = _h.CreateScheduler();

        Assert.Equal(1, await scheduler.StartDueAsync(ct));
        await WaitUntilAsync(() => _h.Source.Requests.Count == 1);
        _h.Time.Advance(TimeSpan.FromHours(2));
        Assert.Equal(0, await scheduler.StartDueAsync(ct));
        Assert.Null(scheduler.NextDue(feed.Id));

        gate.SetResult();
        await scheduler.WhenIdleAsync();

        Assert.Single(_h.Source.Requests);
        Assert.Equal(_h.Time.GetUtcNow() + TimeSpan.FromMinutes(10), scheduler.NextDue(feed.Id));
    }

    [Fact]
    public async Task GlobalConcurrencyIsLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        _h.Options = _h.Options with { MaxConcurrency = 2 };
        for (var i = 0; i < 5; i++)
        {
            await _h.AddFeedAsync($"feed{i}");
        }

        var gate = new TaskCompletionSource();
        var active = 0;
        var peak = 0;
        _h.Source.Handler = async (_, token) =>
        {
            var now = Interlocked.Increment(ref active);
            InterlockedMax(ref peak, now);
            await gate.Task.WaitAsync(token);
            Interlocked.Decrement(ref active);
            return new FeedFetched(null, [], CacheValidators.None);
        };
        using var scheduler = _h.CreateScheduler();

        Assert.Equal(5, await scheduler.StartDueAsync(ct));
        await WaitUntilAsync(() => _h.Source.Requests.Count == 2);
        await Task.Delay(200, ct);

        Assert.Equal(2, _h.Source.Requests.Count);

        gate.SetResult();
        await scheduler.WhenIdleAsync();

        Assert.Equal(5, _h.Source.Requests.Count);
        Assert.Equal(2, peak);
    }

    [Fact]
    public async Task OnlyDueFeedsAreStarted()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        using var scheduler = _h.CreateScheduler();

        Assert.Equal(1, await scheduler.StartDueAsync(ct));
        await scheduler.WhenIdleAsync();
        _h.Time.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(0, await scheduler.StartDueAsync(ct));
        _h.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await scheduler.StartDueAsync(ct));
        await scheduler.WhenIdleAsync();

        Assert.Equal(2, _h.Source.Requests.Count);
        Assert.NotNull(scheduler.NextDue(feed.Id));
    }

    [Fact]
    public async Task FailuresBackOffUpToTheCapAndRecoverAtTheInterval()
    {
        var feed = await _h.AddFeedAsync();
        _h.Source.Returns(Failure);
        using var scheduler = _h.CreateScheduler();

        var delays = new List<double>();
        for (var i = 0; i < 6; i++)
        {
            delays.Add(await RunDueAndMeasureAsync(scheduler, feed.Id));
        }

        _h.Source.ReturnsItems();
        delays.Add(await RunDueAndMeasureAsync(scheduler, feed.Id));

        Assert.Equal([10, 20, 40, 60, 60, 60, 10], delays);
        Assert.Equal(0, (await _h.ReloadAsync(feed)).Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task CrashingProcessorIsContainedAndBacksOff()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        _h.Source.Handler = (_, _) => throw new InvalidOperationException("boom");
        using var scheduler = _h.CreateScheduler();

        await scheduler.StartDueAsync(ct);
        await scheduler.WhenIdleAsync();

        Assert.Equal(_h.Time.GetUtcNow() + TimeSpan.FromMinutes(10), scheduler.NextDue(feed.Id));
    }

    [Fact]
    public async Task RestartResumesFromStoredHealth()
    {
        var ct = TestContext.Current.CancellationToken;
        var healthy = await _h.AddFeedAsync("healthy");
        var failing = await _h.AddFeedAsync("failing");
        await _h.Feeds.RecordSuccessAsync(healthy.Id, Start - TimeSpan.FromMinutes(3), new(null, null), ct);
        for (var i = 0; i < 3; i++)
        {
            await _h.Feeds.RecordFailureAsync(failing.Id, Start - TimeSpan.FromMinutes(5), "down", ct);
        }

        using var scheduler = _h.CreateScheduler();

        Assert.Equal(0, await scheduler.StartDueAsync(ct));
        Assert.Equal(Start + TimeSpan.FromMinutes(7), scheduler.NextDue(healthy.Id));
        Assert.Equal(Start + TimeSpan.FromMinutes(35), scheduler.NextDue(failing.Id));
    }

    [Fact]
    public async Task IntervalChangeAppliesBeforeTheNextFetch()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync(intervalMinutes: 60);
        using var scheduler = _h.CreateScheduler();
        await scheduler.StartDueAsync(ct);
        await scheduler.WhenIdleAsync();
        var ranAt = _h.Time.GetUtcNow();

        await _h.Feeds.UpdateAsync(feed.Id, feed.Settings with { Interval = TimeSpan.FromMinutes(5) }, ct);
        _h.Time.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(0, await scheduler.StartDueAsync(ct));
        Assert.Equal(ranAt + TimeSpan.FromMinutes(5), scheduler.NextDue(feed.Id));

        await _h.Feeds.UpdateAsync(feed.Id, feed.Settings with { Interval = TimeSpan.FromMinutes(3) }, ct);
        Assert.Equal(1, await scheduler.StartDueAsync(ct));
        await scheduler.WhenIdleAsync();

        await _h.Feeds.UpdateAsync(feed.Id, feed.Settings with { Interval = TimeSpan.FromMinutes(30) }, ct);
        _h.Time.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal(0, await scheduler.StartDueAsync(ct));
        Assert.Equal(2, _h.Source.Requests.Count);
    }

    [Fact]
    public async Task SinkRetryAfterShorterThanTheIntervalBringsTheNextCycleForward()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("seed"));
        using var scheduler = _h.CreateScheduler();
        await scheduler.StartDueAsync(ct);
        await scheduler.WhenIdleAsync();

        _h.Discord.Handler = (_, _) => DeliveryResult.Transient("rate limited", TimeSpan.FromMinutes(2));
        _h.Source.ReturnsItems(Item("a"), Item("seed"));
        _h.Time.Advance(TimeSpan.FromMinutes(10));
        await scheduler.StartDueAsync(ct);
        await scheduler.WhenIdleAsync();

        Assert.Equal(_h.Time.GetUtcNow() + TimeSpan.FromMinutes(2), scheduler.NextDue(feed.Id));

        _h.Discord.Handler = (_, _) => DeliveryResult.Delivered;
        _h.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await scheduler.StartDueAsync(ct));
        await scheduler.WhenIdleAsync();

        Assert.Equal(["a", "a"], _h.Discord.Contents);
        Assert.Equal(_h.Time.GetUtcNow() + TimeSpan.FromMinutes(10), scheduler.NextDue(feed.Id));
    }

    [Fact]
    public async Task FetchAndDeliveryRunEndToEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("a"));
        using var scheduler = _h.CreateScheduler();
        await scheduler.StartDueAsync(ct);
        await scheduler.WhenIdleAsync();

        _h.Source.ReturnsItems(Item("b"), Item("a"));
        _h.Time.Advance(TimeSpan.FromMinutes(10));
        await scheduler.StartDueAsync(ct);
        await scheduler.WhenIdleAsync();

        Assert.Equal(["b"], _h.Discord.Contents);
    }

    private async Task<double> RunDueAndMeasureAsync(FeedScheduler scheduler, long feedId)
    {
        var ct = TestContext.Current.CancellationToken;
        if (scheduler.NextDue(feedId) is { } due)
        {
            _h.Time.SetUtcNow(due);
        }

        Assert.Equal(1, await scheduler.StartDueAsync(ct));
        await scheduler.WhenIdleAsync();
        return (scheduler.NextDue(feedId)!.Value - _h.Time.GetUtcNow()).TotalMinutes;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition not met in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
