using System.Net;
using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using InformadoRSS.Storage;
using static InformadoRSS.Tests.Scheduling.PipelineHarness;

namespace InformadoRSS.Tests.Scheduling;

public sealed class FeedProcessorTests : IAsyncLifetime
{
    private PipelineHarness _h = null!;

    public async ValueTask InitializeAsync() => _h = await CreateAsync();

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    private static DateTimeOffset Day(int day) => new(2026, 10, day, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FirstFetchMarksExistingItemsSeenWithoutSending()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("b"), Item("a"));

        await _h.RunCycleAsync(feed);

        Assert.Empty(_h.Discord.Attempts);

        _h.Source.ReturnsItems(FakeSource.V2, Item("c"), Item("b"), Item("a"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["c"], _h.Discord.Contents);
    }

    [Fact]
    public async Task NewRouteOnExistingFeedIsSeededAndForcesFullFetch()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var first = await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("a"));
        await _h.RunCycleAsync(feed);

        var second = await _h.AddRouteAsync(feed, destinations: PipelineHarness.NtfyTopic("t"));
        _h.Source.ReturnsItems(FakeSource.V2, Item("b"), Item("a"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["b"], _h.Discord.Contents);
        Assert.Empty(_h.Ntfy.Attempts);
        Assert.Null(_h.Source.Requests.Last().Validators);
        Assert.True(await _h.SeenItems.IsSeededAsync(second.Id, ct));
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(first.Id, ["a", "b"], ct));

        await _h.RunCycleAsync(feed);

        Assert.Equal(FakeSource.V2, _h.Source.Requests.Last().Validators);
        Assert.Equal(["b"], _h.Discord.Contents);
        Assert.Empty(_h.Ntfy.Attempts);
    }

    [Fact]
    public async Task ItemsAreDeliveredOncePerRoute()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        await _h.AddRouteAsync(feed, destinations: PipelineHarness.NtfyTopic("t"));
        _h.Source.ReturnsItems(Item("a"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(Item("b"), Item("a"));
        await _h.RunCycleAsync(feed);
        await _h.RunCycleAsync(feed);

        Assert.Equal(["b"], _h.Discord.Contents);
        Assert.Equal(["b"], _h.Ntfy.Contents);
    }

    [Fact]
    public async Task ItemsAreSentOldestFirstByPublicationDate()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(Item("mid", Day(2)), Item("new", Day(3)), Item("old", Day(1)), Item("seed", Day(1)));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["old", "mid", "new"], _h.Discord.Contents);
    }

    [Fact]
    public async Task ItemsWithoutDatesAreSentInReversedFeedOrder()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(Item("third", Day(1)), Item("second"), Item("first", Day(3)), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["first", "second", "third"], _h.Discord.Contents);
    }

    [Fact]
    public async Task PerCycleCapLeavesTheRestForTheNextCycle()
    {
        _h.Options = _h.Options with { MaxItemsPerRoute = 2 };
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(FakeSource.V2, Item("e", Day(5)), Item("d", Day(4)), Item("c", Day(3)), Item("b", Day(2)), Item("a", Day(1)));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a", "b"], _h.Discord.Contents);
        Assert.Equal(FakeSource.V1.ETag, (await _h.ReloadAsync(feed)).Validators.ETag);

        await _h.RunCycleAsync(feed);
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a", "b", "c", "d", "e"], _h.Discord.Contents);
        Assert.Equal(FakeSource.V2.ETag, (await _h.ReloadAsync(feed)).Validators.ETag);
    }

    [Fact]
    public async Task TransientFailureStopsTheRouteAndRetriesNextCycle()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Discord.Handler = (_, message) => message.Content == "b" ? DeliveryResult.Transient("rate limited") : DeliveryResult.Delivered;
        _h.Source.ReturnsItems(Item("c", Day(3)), Item("b", Day(2)), Item("a", Day(1)));
        var outcome = await _h.RunCycleAsync(feed);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["a", "b"], _h.Discord.Contents);
        Assert.Equal(["b", "c"], await _h.SeenItems.FilterUnseenAsync(route.Id, ["a", "b", "c"], ct));

        _h.Discord.Handler = (_, _) => DeliveryResult.Delivered;
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a", "b", "b", "c"], _h.Discord.Contents);
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(route.Id, ["a", "b", "c"], ct));
    }

    [Fact]
    public async Task ItemIsSeenOnlyAfterEveryDestinationIsDone()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, destinations: [DiscordChannel("1"), NtfyTopic("t")]);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Ntfy.Handler = (_, _) => DeliveryResult.Transient("down");
        _h.Source.ReturnsItems(Item("a"), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], await _h.SeenItems.FilterUnseenAsync(route.Id, ["a"], ct));

        _h.Ntfy.Handler = (_, _) => DeliveryResult.Delivered;
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], _h.Discord.Contents);
        Assert.Equal(["a", "a"], _h.Ntfy.Contents);
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(route.Id, ["a"], ct));
    }

    [Fact]
    public async Task FailingDestinationDoesNotHoldTheItemBackFromTheOthers()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed, destinations: [NtfyTopic("t"), DiscordChannel("1"), DiscordChannel("2")]);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Ntfy.Handler = (_, _) => DeliveryResult.Transient("down");
        _h.Discord.Handler = (destination, _) => destination.Parameters["channelId"] == "2"
            ? DeliveryResult.Transient("down")
            : DeliveryResult.Delivered;
        _h.Source.ReturnsItems(Item("b", Day(2)), Item("a", Day(1)), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], _h.Ntfy.Contents);
        Assert.Equal([("1", "a"), ("2", "a")], DiscordAttempts());

        _h.Ntfy.Handler = (_, _) => DeliveryResult.Delivered;
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a", "a"], _h.Ntfy.Contents);
        Assert.Equal([("1", "a"), ("2", "a"), ("2", "a")], DiscordAttempts());

        _h.Discord.Handler = (_, _) => DeliveryResult.Delivered;
        await _h.RunCycleAsync(feed);
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a", "a", "b"], _h.Ntfy.Contents);
        Assert.Equal([("1", "a"), ("2", "a"), ("2", "a"), ("2", "a"), ("1", "b"), ("2", "b")], DiscordAttempts());
    }

    [Fact]
    public async Task RejectedMessageIsSkippedForThatDestinationWithoutFlaggingIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, destinations: [DiscordChannel("1"), NtfyTopic("t")]);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Discord.Handler = (_, message) => message.Content == "a"
            ? DeliveryResult.Rejected("Invalid Form Body")
            : DeliveryResult.Delivered;
        _h.Source.ReturnsItems(Item("b", Day(2)), Item("a", Day(1)), Item("seed"));
        await _h.RunCycleAsync(feed);
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a", "b"], _h.Discord.Contents);
        Assert.Equal(["a", "b"], _h.Ntfy.Contents);
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(route.Id, ["a", "b"], ct));
        Assert.Empty(await _h.Routes.ListBrokenDestinationsAsync(ct));
    }

    [Fact]
    public async Task RouteSeededOnAnEmptyFeedSendsTheFirstItemThatAppears()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems();
        await _h.RunCycleAsync(feed);

        Assert.True(await _h.SeenItems.IsSeededAsync(route.Id, ct));

        _h.Source.ReturnsItems(FakeSource.V2, Item("a"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], _h.Discord.Contents);
        Assert.Equal(FakeSource.V1, _h.Source.Requests.Last().Validators);
    }

    [Fact]
    public async Task SinkRetryAfterHoldsTheDestinationBackAndIsReported()
    {
        _h.Options = _h.Options with { MaxRetryAfter = TimeSpan.FromMinutes(30) };
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed, destinations: [DiscordChannel("1"), NtfyTopic("t")]);
        await _h.AddRouteAsync(feed, destinations: NtfyTopic("other"));
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Discord.Handler = (_, _) => DeliveryResult.Transient("rate limited", TimeSpan.FromMinutes(2));
        _h.Source.ReturnsItems(Item("a"), Item("seed"));
        var outcome = await _h.RunCycleAsync(feed);

        Assert.Equal(TimeSpan.FromMinutes(2), outcome.DeliveryRetryAfter);
        Assert.Single(_h.Discord.Attempts);
        Assert.Equal(2, _h.Ntfy.Attempts.Count);

        _h.Discord.Handler = (_, _) => DeliveryResult.Delivered;
        _h.Time.Advance(TimeSpan.FromMinutes(1));
        outcome = await _h.RunCycleAsync(feed);

        Assert.Equal(TimeSpan.FromMinutes(1), outcome.DeliveryRetryAfter);
        Assert.Single(_h.Discord.Attempts);

        _h.Time.Advance(TimeSpan.FromMinutes(1));
        outcome = await _h.RunCycleAsync(feed);

        Assert.Equal(FeedOutcome.Success, outcome);
        Assert.Equal(["a", "a"], _h.Discord.Contents);
        Assert.Equal(2, _h.Ntfy.Attempts.Count);
    }

    [Fact]
    public async Task SinkRetryAfterIsCappedByMaxRetryAfter()
    {
        _h.Options = _h.Options with { MaxRetryAfter = TimeSpan.FromMinutes(5) };
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Discord.Handler = (_, _) => DeliveryResult.Transient("rate limited", TimeSpan.FromDays(3));
        _h.Source.ReturnsItems(Item("a"), Item("seed"));
        var outcome = await _h.RunCycleAsync(feed);

        Assert.Equal(TimeSpan.FromMinutes(5), outcome.DeliveryRetryAfter);
        Assert.Equal(TimeSpan.FromMinutes(5), _h.Backoff.Remaining(route.Id, 0));
    }

    private List<(string Channel, string Content)> DiscordAttempts() =>
        [.. _h.Discord.Attempts.Select(attempt => (attempt.Destination.Parameters["channelId"], attempt.Content))];

    [Fact]
    public async Task UnregisteredSinkKeepsItemsUnseen()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, destinations: new Destination("telegram", new Dictionary<string, string>()));
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(Item("a"), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], await _h.SeenItems.FilterUnseenAsync(route.Id, ["a"], ct));
    }

    [Fact]
    public async Task PermanentFailureMarksItemSeenAndFlagsDestinationUntilItWorksAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, destinations: [NtfyTopic("t"), DiscordChannel("deleted")]);
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Discord.Handler = (_, _) => DeliveryResult.Permanent("Unknown Channel");
        _h.Source.ReturnsItems(Item("a"), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], _h.Ntfy.Contents);
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(route.Id, ["a"], ct));
        Assert.Equal([(0, null), (1, "Unknown Channel")], await BrokenDestinationsAsync(route.Id));

        _h.Discord.Handler = (_, _) => DeliveryResult.Delivered;
        _h.Source.ReturnsItems(Item("b"), Item("a"), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal([(0, null), (1, null)], await BrokenDestinationsAsync(route.Id));
    }

    [Fact]
    public async Task FilteredOutItemsAreMarkedSeen()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, """{"type":"condition","field":"title","operator":"contains","value":"keep"}""");
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(Item("a", title: "keep me"), Item("b", title: "drop me"), Item("seed"));
        await _h.RunCycleAsync(feed);

        Assert.Equal(["a"], _h.Discord.Contents);
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(route.Id, ["a", "b"], ct));
    }

    [Fact]
    public async Task RegexTimeoutSkipsOnlyThatItem()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, """{"type":"condition","field":"title","operator":"regex","value":"^(a+)+$"}""");
        _h.Source.ReturnsItems(Item("seed"));
        await _h.RunCycleAsync(feed);

        _h.Source.ReturnsItems(Item("slow", title: new string('a', 64) + "!"), Item("fast", title: "aaaa"), Item("seed"));
        var outcome = await _h.RunCycleAsync(feed);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["fast"], _h.Discord.Contents);
        Assert.Equal(["slow"], await _h.SeenItems.FilterUnseenAsync(route.Id, ["slow", "fast"], ct));
    }

    [Fact]
    public async Task DisabledRoutesAreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, enabled: false);
        _h.Source.ReturnsItems(Item("a"));

        await _h.RunCycleAsync(feed);

        Assert.False(await _h.SeenItems.IsSeededAsync(route.Id, ct));
        Assert.Empty(_h.Discord.Attempts);
    }

    [Fact]
    public async Task StoredValidatorsAreSentAndNotModifiedCountsAsSuccess()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed);
        _h.Source.ReturnsItems(Item("a"));
        await _h.RunCycleAsync(feed);
        _h.Source.Returns(new FeedFetchFailed(new FetchError(FetchErrorKind.Timeout, "timed out")));
        await _h.RunCycleAsync(feed);

        _h.Source.Returns(new FeedNotModified(FakeSource.V2));
        var outcome = await _h.RunCycleAsync(feed);

        Assert.Equal(FeedOutcome.Success, outcome);
        Assert.Equal(FakeSource.V1, _h.Source.Requests.Last().Validators);
        var stored = await _h.ReloadAsync(feed);
        Assert.Equal(0, stored.Health.ConsecutiveFailures);
        Assert.Equal(new FeedValidators(FakeSource.V2.ETag, null), stored.Validators);
    }

    [Fact]
    public async Task FailuresAreRecordedAndReportRetryAfter()
    {
        var feed = await _h.AddFeedAsync();
        _h.Source.Returns(new FeedFetchFailed(
            new FetchError(FetchErrorKind.HttpStatus, "404 Not Found", HttpStatusCode.NotFound, TimeSpan.FromMinutes(30))));

        await _h.RunCycleAsync(feed);
        var outcome = await _h.RunCycleAsync(feed);

        Assert.Equal(new FeedOutcome(false, 2, TimeSpan.FromMinutes(30)), outcome);
        var stored = await _h.ReloadAsync(feed);
        Assert.Equal(2, stored.Health.ConsecutiveFailures);
        Assert.Equal("404 Not Found", stored.Health.LastError);
        Assert.Equal(Start, stored.Health.LastErrorAt);
    }

    private async Task<List<(long Position, string? Reason)>> BrokenDestinationsAsync(long routeId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await _h.Database.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT position, broken_reason FROM destinations WHERE route_id = $routeId ORDER BY position";
        command.Parameters.AddWithValue("$routeId", routeId);
        var result = new List<(long, string?)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        }

        return result;
    }
}
