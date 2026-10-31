using System.Net;
using System.Text.Json;
using InformadoRSS.Alerts;
using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;
using InformadoRSS.Tests.Scheduling;
using InformadoRSS.Tests.Sinks.Discord;
using InformadoRSS.Tests.Sinks.Ntfy;
using InformadoRSS.Tests.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace InformadoRSS.Tests.Alerts;

public sealed class AlertMonitorTests : IAsyncLifetime
{
    private const string AlertChannel = "4242";
    private const string AlertRoute = $"POST channels/{AlertChannel}/messages";

    private static readonly DateTimeOffset Start = new(2026, 10, 18, 5, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDatabase _db = new();
    private readonly ManualTimeProvider _time = new(Start);
    private readonly FakeDiscordApi _discord = new();
    private readonly FakeNtfyServer _ntfy = new();
    private SqliteAlertStateStore _states = null!;
    private Feed _feed = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.InitializeAsync();
        _states = new SqliteAlertStateStore(_db.Database);
        _feed = await _db.Feeds.AddAsync(
            new FeedSettings(new Uri("https://www.youtube.com/feeds/videos.xml?channel_id=UC1"), "PolHostRex", TimeSpan.FromMinutes(10)),
            ct);
        await _db.Settings.SetAsync(AlertSettings.DiscordChannelIdKey, AlertChannel, ct);
        _discord.On(AlertRoute, """{"id":"1"}""");
    }

    public async ValueTask DisposeAsync()
    {
        _discord.Dispose();
        _ntfy.Dispose();
        await _db.DisposeAsync();
    }

    private AlertMonitor CreateMonitor()
    {
        var discord = new DiscordRestClient(
            new HttpClient(_discord), new MutableTokenProvider("token"), new DiscordRateLimiter(_time), new DiscordClientOptions(), _time);
        var ntfy = new NtfyClient(new HttpClient(_ntfy), _db.Settings);
        var sender = new AlertSender(discord, ntfy, NullLogger<AlertSender>.Instance);
        return new AlertMonitor(_db.Feeds, new SqliteAlertStateStore(_db.Database), _db.Settings, sender, _time);
    }

    private Task NotifyAsync() =>
        CreateMonitor().OnFeedProcessedAsync(_feed, FeedOutcome.Success, TestContext.Current.CancellationToken);

    private async Task FailAsync(TimeSpan after)
    {
        _time.Advance(after);
        await _db.Feeds.RecordFailureAsync(_feed.Id, _time.GetUtcNow(), "HTTP 404 Not Found", TestContext.Current.CancellationToken);
        await NotifyAsync();
    }

    private async Task SucceedAsync(TimeSpan after)
    {
        _time.Advance(after);
        await _db.Feeds.RecordSuccessAsync(_feed.Id, _time.GetUtcNow(), FeedValidators.None, TestContext.Current.CancellationToken);
        await NotifyAsync();
    }

    private List<string> AlertTitles(string route = AlertRoute) =>
        [.. _discord.Requests
            .Zip(_discord.RequestedRoutes)
            .Where(pair => pair.Second == route)
            .Select(pair => JsonDocument.Parse(pair.First.Body).RootElement.GetProperty("embeds")[0].GetProperty("title").GetString()!)];

    private async Task<Route> AddRouteAsync(params Destination[] destinations) =>
        (await _db.Routes.AddAsync(
            _feed.Id, new RouteSettings("Videos", null, null, true, destinations), TestContext.Current.CancellationToken))!;

    private static Destination Channel(string id) =>
        new(DiscordSink.SinkName, new Dictionary<string, string> { [DiscordSink.ChannelIdParameter] = id });

    [Fact]
    public async Task DailyYouTubeOutageDoesNotAlert()
    {
        await FailAsync(TimeSpan.Zero);
        for (var i = 0; i < 18; i++)
        {
            await FailAsync(TimeSpan.FromMinutes(30));
        }

        await SucceedAsync(TimeSpan.FromMinutes(10));
        await FailAsync(TimeSpan.FromHours(20));
        await FailAsync(TimeSpan.FromHours(3));
        await SucceedAsync(TimeSpan.FromMinutes(10));

        Assert.Empty(_discord.Requests);
        Assert.Null(await _states.GetFeedStateAsync(_feed.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LongOutageAlertsOnceThenRecoversOnce()
    {
        await FailAsync(TimeSpan.Zero);
        for (var i = 0; i < 23; i++)
        {
            await FailAsync(TimeSpan.FromHours(1));
        }

        Assert.Empty(_discord.Requests);

        await FailAsync(TimeSpan.FromMinutes(59));
        Assert.Empty(_discord.Requests);

        await FailAsync(TimeSpan.FromMinutes(1));
        await FailAsync(TimeSpan.FromHours(1));
        await FailAsync(TimeSpan.FromHours(1));

        Assert.Equal(["Feed failing: PolHostRex"], AlertTitles());
        var message = JsonDocument.Parse(_discord.Requests[0].Body).RootElement.GetProperty("embeds")[0];
        Assert.Contains("failing for 1d (26 failures in a row)", message.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("HTTP 404 Not Found", message.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal(AlertSender.ProblemColor, message.GetProperty("color").GetInt32());

        await SucceedAsync(TimeSpan.FromMinutes(10));
        await SucceedAsync(TimeSpan.FromMinutes(10));

        Assert.Equal(["Feed failing: PolHostRex", "Feed recovered: PolHostRex"], AlertTitles());
        Assert.Null(await _states.GetFeedStateAsync(_feed.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AlertedStateSurvivesRestart()
    {
        var ct = TestContext.Current.CancellationToken;
        await FailAsync(TimeSpan.Zero);
        var pastThreshold = AlertSettings.DefaultFeedBrokenAfter + TimeSpan.FromHours(1);
        await FailAsync(pastThreshold);
        Assert.Single(_discord.Requests);

        var state = await new SqliteAlertStateStore(_db.Database).GetFeedStateAsync(_feed.Id, ct);
        Assert.Equal(new FeedAlertState(Start, Start + pastThreshold), state);

        await FailAsync(TimeSpan.FromHours(1));
        await FailAsync(TimeSpan.FromHours(1));

        Assert.Single(_discord.Requests);
    }

    [Fact]
    public async Task ThresholdComesFromSettings()
    {
        await _db.Settings.SetAsync(AlertSettings.FeedBrokenAfterMinutesKey, "60", TestContext.Current.CancellationToken);

        await FailAsync(TimeSpan.Zero);
        await FailAsync(TimeSpan.FromMinutes(59));
        Assert.Empty(_discord.Requests);

        await FailAsync(TimeSpan.FromMinutes(1));
        Assert.Equal(["Feed failing: PolHostRex"], AlertTitles());
    }

    [Fact]
    public async Task TemporaryDeliveryFailureIsRetriedOnNextCycle()
    {
        var ct = TestContext.Current.CancellationToken;
        _discord.On(AlertRoute, () => FakeDiscordApi.Json(HttpStatusCode.BadGateway, """{"message":"bad gateway","code":0}"""));

        await FailAsync(TimeSpan.Zero);
        await FailAsync(AlertSettings.DefaultFeedBrokenAfter);

        Assert.Single(_discord.Requests);
        Assert.Null((await _states.GetFeedStateAsync(_feed.Id, ct))!.AlertedAt);

        _discord.On(AlertRoute, """{"id":"1"}""");
        await FailAsync(TimeSpan.FromHours(1));
        await FailAsync(TimeSpan.FromHours(1));

        Assert.Equal(2, _discord.Requests.Count);
        Assert.NotNull((await _states.GetFeedStateAsync(_feed.Id, ct))!.AlertedAt);
    }

    [Fact]
    public async Task PermanentDeliveryFailureIsNotRetried()
    {
        _discord.On(AlertRoute, () => FakeDiscordApi.Json(HttpStatusCode.NotFound, """{"message":"Unknown Channel","code":10003}"""));

        await FailAsync(TimeSpan.Zero);
        await FailAsync(AlertSettings.DefaultFeedBrokenAfter);
        await FailAsync(TimeSpan.FromHours(1));

        Assert.Single(_discord.Requests);
    }

    [Fact]
    public async Task WithoutTargetAlertsAreOnlyLoggedButStillTracked()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.Settings.SetAsync(AlertSettings.DiscordChannelIdKey, null, ct);

        await FailAsync(TimeSpan.Zero);
        await FailAsync(AlertSettings.DefaultFeedBrokenAfter);

        Assert.NotNull((await _states.GetFeedStateAsync(_feed.Id, ct))!.AlertedAt);

        await SucceedAsync(TimeSpan.FromMinutes(10));

        Assert.Empty(_discord.Requests);
        Assert.Null(await _states.GetFeedStateAsync(_feed.Id, ct));
    }

    [Fact]
    public async Task DirectMessageTargetOpensDmChannel()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.Settings.SetAsync(AlertSettings.DiscordChannelIdKey, null, ct);
        await _db.Settings.SetAsync(AlertSettings.DiscordUserIdKey, "77", ct);
        _discord
            .On("POST users/@me/channels", """{"id":"555","type":1}""")
            .On("POST channels/555/messages", """{"id":"1"}""");

        await FailAsync(TimeSpan.Zero);
        await FailAsync(AlertSettings.DefaultFeedBrokenAfter);

        Assert.Equal(["POST users/@me/channels", "POST channels/555/messages"], _discord.RequestedRoutes);
        Assert.Equal("77", JsonDocument.Parse(_discord.Requests[0].Body).RootElement.GetProperty("recipient_id").GetString());
        Assert.Equal(["Feed failing: PolHostRex"], AlertTitles("POST channels/555/messages"));
    }

    [Fact]
    public async Task DirectMessageToUnknownUserIsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.Settings.SetAsync(AlertSettings.DiscordChannelIdKey, null, ct);
        await _db.Settings.SetAsync(AlertSettings.DiscordUserIdKey, "77", ct);
        _discord.On(
            "POST users/@me/channels",
            () => FakeDiscordApi.Json(HttpStatusCode.BadRequest, """{"message":"Invalid Recipient(s)","code":50033}"""));

        await FailAsync(TimeSpan.Zero);
        await FailAsync(AlertSettings.DefaultFeedBrokenAfter);
        await FailAsync(TimeSpan.FromHours(1));

        Assert.Equal(["POST users/@me/channels"], _discord.RequestedRoutes);
    }

    [Fact]
    public async Task NtfyTargetPublishesWithPriorityAndTags()
    {
        var ct = TestContext.Current.CancellationToken;
        await _db.Settings.SetAsync(AlertSettings.TargetKey, "ntfy", ct);
        await _db.Settings.SetAsync(AlertSettings.NtfyTopicKey, "servidor", ct);
        await _db.Settings.SetAsync(NtfySettings.UrlKey, "http://ntfy.local:8094", ct);

        await FailAsync(TimeSpan.Zero);
        await FailAsync(AlertSettings.DefaultFeedBrokenAfter);
        await SucceedAsync(TimeSpan.FromMinutes(10));

        Assert.Empty(_discord.Requests);
        Assert.Equal(2, _ntfy.Requests.Count);
        var (broken, body) = _ntfy.Requests[0];
        Assert.Equal("http://ntfy.local:8094/servidor", broken.RequestUri!.AbsoluteUri);
        Assert.Equal("Feed failing: PolHostRex", FakeNtfyServer.Header(broken, "Title"));
        Assert.Equal("4", FakeNtfyServer.Header(broken, "Priority"));
        Assert.Equal("warning", FakeNtfyServer.Header(broken, "Tags"));
        Assert.StartsWith("PolHostRex has been failing for 1d", body, StringComparison.Ordinal);
        var recovered = _ntfy.Requests[1].Request;
        Assert.Equal("Feed recovered: PolHostRex", FakeNtfyServer.Header(recovered, "Title"));
        Assert.Equal("white_check_mark", FakeNtfyServer.Header(recovered, "Tags"));
    }

    [Fact]
    public async Task BrokenDestinationAlertsOncePerTransition()
    {
        var ct = TestContext.Current.CancellationToken;
        var route = await AddRouteAsync(Channel("1"), Channel("2"));

        await _db.Routes.SetDestinationBrokenAsync(route.Id, 1, _time.GetUtcNow(), "Discord channel 2 is not usable by the bot", ct);
        await NotifyAsync();
        await NotifyAsync();
        await _db.Routes.SetDestinationBrokenAsync(route.Id, 1, _time.GetUtcNow(), "still broken", ct);
        await NotifyAsync();

        Assert.Equal(["Destination broken: Videos"], AlertTitles());
        var description = JsonDocument.Parse(_discord.Requests[0].Body).RootElement.GetProperty("embeds")[0]
            .GetProperty("description").GetString();
        Assert.Contains("Discord channel 2 (destination 2)", description, StringComparison.Ordinal);
        Assert.Contains("not usable by the bot", description, StringComparison.Ordinal);

        await _db.Routes.SetDestinationBrokenAsync(route.Id, 1, _time.GetUtcNow(), null, ct);
        await NotifyAsync();
        await NotifyAsync();

        Assert.Equal(["Destination broken: Videos", "Destination working again: Videos"], AlertTitles());
        Assert.Empty(await _states.ListDestinationStatesAsync(_feed.Id, ct));
    }

    [Fact]
    public async Task ReplacingDestinationsForgetsTheirAlerts()
    {
        var ct = TestContext.Current.CancellationToken;
        var route = await AddRouteAsync(Channel("1"));
        await _db.Routes.SetDestinationBrokenAsync(route.Id, 0, _time.GetUtcNow(), "gone", ct);
        await NotifyAsync();

        await _db.Routes.UpdateAsync(route.Id, route.Settings, ct);
        await NotifyAsync();

        Assert.Equal(["Destination broken: Videos"], AlertTitles());
        Assert.Empty(await _states.ListDestinationStatesAsync(_feed.Id, ct));
    }

    [Fact]
    public async Task DeletedFeedIsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await FailAsync(TimeSpan.Zero);
        await _db.Feeds.DeleteAsync(_feed.Id, ct);

        _time.Advance(AlertSettings.DefaultFeedBrokenAfter + TimeSpan.FromHours(1));
        await NotifyAsync();

        Assert.Empty(_discord.Requests);
        Assert.Null(await _states.GetFeedStateAsync(_feed.Id, ct));
    }
}
