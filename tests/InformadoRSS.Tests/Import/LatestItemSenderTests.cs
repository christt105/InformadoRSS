using InformadoRSS.Core;
using InformadoRSS.Import;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Tests.Scheduling;
using static InformadoRSS.Tests.Scheduling.PipelineHarness;

namespace InformadoRSS.Tests.Import;

public sealed class LatestItemSenderTests : IAsyncLifetime
{
    private PipelineHarness _h = null!;

    public async ValueTask InitializeAsync() => _h = await CreateAsync();

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LatestItemSender Sender => new(_h.Source, _h.Feeds, _h.Routes, new FakeRenderer(), [_h.Discord, _h.Ntfy]);

    private static DateTimeOffset Day(int day) => new(2026, 10, day, 12, 0, 0, TimeSpan.Zero);

    private const string NotEarlyAccess =
        """{"type":"condition","field":"title","operator":"contains","value":"Early access","negate":true}""";

    [Fact]
    public async Task SendsTheNewestMatchingItemWithoutMarkingItSeen()
    {
        var feed = await _h.AddFeedAsync();
        var route = await _h.AddRouteAsync(feed, NotEarlyAccess);
        _h.Source.ReturnsItems(Item("new", Day(3), "Early access 1.10"), Item("mid", Day(2), "1.9"), Item("old", Day(1), "1.8"));

        var result = Assert.Single(await Sender.SendAsync(new SendLatestOptions(), Ct));

        Assert.Equal(SendLatestStatus.Sent, result.Status);
        Assert.Equal("1.9", result.ItemTitle);
        Assert.Equal(["mid"], _h.Discord.Contents);
        Assert.False(await _h.SeenItems.IsSeededAsync(route.Id, Ct));
        Assert.Null(Assert.Single(_h.Source.Requests).Validators);
    }

    [Fact]
    public async Task ChannelOverrideReplacesEveryDestination()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed, destinations: [DiscordChannel("1"), NtfyTopic("t")]);
        _h.Source.ReturnsItems(Item("a"));

        await Sender.SendAsync(new SendLatestOptions("42"), Ct);

        var attempt = Assert.Single(_h.Discord.Attempts);
        Assert.Equal("42", attempt.Destination.Parameters[DiscordSink.ChannelIdParameter]);
        Assert.Empty(_h.Ntfy.Attempts);
    }

    [Fact]
    public async Task ReportsFailuresAndSkipsDisabledRoutesUnlessAsked()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed, enabled: false);
        _h.Source.ReturnsItems(Item("a"));
        _h.Discord.Handler = (_, _) => DeliveryResult.Permanent("missing access");

        Assert.Empty(await Sender.SendAsync(new SendLatestOptions(), Ct));

        var result = Assert.Single(await Sender.SendAsync(new SendLatestOptions(IncludeDisabled: true), Ct));
        Assert.Equal(SendLatestStatus.Failed, result.Status);
        Assert.Contains("missing access", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsWhenNothingMatches()
    {
        var feed = await _h.AddFeedAsync();
        await _h.AddRouteAsync(feed, NotEarlyAccess);
        _h.Source.ReturnsItems(Item("a", title: "Early access"));

        var result = Assert.Single(await Sender.SendAsync(new SendLatestOptions(), Ct));

        Assert.Equal(SendLatestStatus.NoMatchingItem, result.Status);
        Assert.Empty(_h.Discord.Attempts);
    }
}
