using System.Globalization;
using System.Net;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Tests.Scheduling;
using static InformadoRSS.Tests.Sinks.Discord.DiscordFixtures;

namespace InformadoRSS.Tests.Sinks.Discord;

public sealed class DiscordDirectoryTests : IDisposable
{
    private const string GuildsRoute = "GET users/@me/guilds?limit=200";
    private const string ChannelsRoute = $"GET guilds/{GuildId}/channels";
    private const string GuildRoute = $"GET guilds/{GuildId}";
    private const string MemberRoute = $"GET guilds/{GuildId}/members/{BotId}";
    private const string UserRoute = "GET users/@me";

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly MutableTokenProvider _token = new("token");
    private readonly FakeDiscordApi _api = new();
    private readonly DiscordDirectoryCache _cache;

    public DiscordDirectoryTests()
    {
        _cache = new DiscordDirectoryCache(_time, new DiscordDirectoryOptions { CacheDuration = TimeSpan.FromMinutes(1) });
        _api.On(UserRoute, CurrentUser)
            .On(GuildsRoute, Guilds)
            .On(ChannelsRoute, Channels)
            .On(GuildRoute, Guild())
            .On(MemberRoute, Member());
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose() => _api.Dispose();

    private DiscordDirectory CreateDirectory()
    {
        var client = new DiscordRestClient(
            new HttpClient(_api),
            _token,
            new DiscordRateLimiter(_time),
            new DiscordClientOptions(),
            _time);
        return new DiscordDirectory(client, _token, _cache, _time);
    }

    private int CountRequests(string route) => _api.RequestedRoutes.Count(requested => requested == route);

    [Fact]
    public async Task ListsGuildsSortedByNameWithIconUrls()
    {
        var result = await CreateDirectory().GetGuildsAsync(refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                new DiscordGuildSummary("60", "animated", "https://cdn.discordapp.com/icons/60/a_def.gif"),
                new DiscordGuildSummary("100", "los panas", "https://cdn.discordapp.com/icons/100/abc.png"),
                new DiscordGuildSummary("50", "Zanahorios", null),
            ],
            result.Value);
        var (request, _) = Assert.Single(_api.Requests);
        Assert.Equal("Bot token", request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task FollowsGuildPagesUntilAShortPage()
    {
        var firstPage = "[" + string.Join(',', Enumerable.Range(1, 200).Select(id => $$"""{"id":"{{id.ToString(CultureInfo.InvariantCulture)}}","name":"g"}""")) + "]";
        _api.On(GuildsRoute, firstPage).On("GET users/@me/guilds?limit=200&after=200", """[{"id":"201","name":"last"}]""");

        var result = await CreateDirectory().GetGuildsAsync(refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.Value.Count);
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task ListsPostableChannelsInDiscordOrder()
    {
        var result = await CreateDirectory().GetChannelsAsync(GuildId, refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                new DiscordChannelSummary(GeneralId, "general", "text", null, null, 1, true),
                new DiscordChannelSummary(NewsId, "news", "announcement", null, null, 2, false),
                new DiscordChannelSummary(HiddenId, "hidden", "text", CategoryId, "Feeds", 2, false),
                new DiscordChannelSummary(AlertsId, "alerts", "text", CategoryId, "Feeds", 5, true),
            ],
            result.Value);
    }

    [Fact]
    public async Task CachesResponsesUntilTheyExpire()
    {
        var directory = CreateDirectory();

        await directory.GetChannelsAsync(GuildId, refresh: false, Cancel);
        _time.Advance(TimeSpan.FromSeconds(59));
        await directory.GetChannelsAsync(GuildId, refresh: false, Cancel);
        Assert.Equal(1, CountRequests(ChannelsRoute));

        _time.Advance(TimeSpan.FromSeconds(2));
        await directory.GetChannelsAsync(GuildId, refresh: false, Cancel);
        Assert.Equal(2, CountRequests(ChannelsRoute));
    }

    [Fact]
    public async Task CacheIsSharedBetweenDirectoryInstances()
    {
        await CreateDirectory().GetGuildsAsync(refresh: false, Cancel);
        await CreateDirectory().GetGuildsAsync(refresh: false, Cancel);

        Assert.Equal(1, CountRequests(GuildsRoute));
    }

    [Fact]
    public async Task RefreshBypassesTheCache()
    {
        var directory = CreateDirectory();

        await directory.GetGuildsAsync(refresh: false, Cancel);
        await directory.GetGuildsAsync(refresh: true, Cancel);
        await directory.GetGuildsAsync(refresh: false, Cancel);

        Assert.Equal(2, CountRequests(GuildsRoute));
    }

    [Fact]
    public async Task ChangingTheTokenDropsTheCache()
    {
        var directory = CreateDirectory();

        await directory.GetGuildsAsync(refresh: false, Cancel);
        _token.Token = "other";
        await directory.GetGuildsAsync(refresh: false, Cancel);

        Assert.Equal(2, CountRequests(GuildsRoute));
    }

    [Fact]
    public async Task FailuresAreNotCached()
    {
        var calls = 0;
        _api.On(GuildsRoute, () => ++calls == 1 ? FakeDiscordApi.Json(HttpStatusCode.InternalServerError, "") : FakeDiscordApi.Json(HttpStatusCode.OK, Guilds));
        var directory = CreateDirectory();

        var failed = await directory.GetGuildsAsync(refresh: false, Cancel);
        var succeeded = await directory.GetGuildsAsync(refresh: false, Cancel);

        Assert.Equal(500, failed.Failure?.StatusCode);
        Assert.True(succeeded.IsSuccess);
    }

    [Fact]
    public async Task UnreadableResponseIsAFailure()
    {
        _api.On(GuildsRoute, "{\"not\":\"a list\"}");

        var result = await CreateDirectory().GetGuildsAsync(refresh: false, Cancel);

        Assert.False(result.IsSuccess);
        Assert.Equal(DiscordApiOutcome.HttpError, result.Failure.Outcome);
        Assert.Contains("unexpected", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingTokenSendsNothing()
    {
        _token.Token = null;
        var directory = CreateDirectory();

        var guilds = await directory.GetGuildsAsync(refresh: false, Cancel);
        var permissions = await directory.CheckPermissionsAsync(GuildId, GeneralId, refresh: false, Cancel);

        Assert.Equal(DiscordApiOutcome.MissingToken, guilds.Failure?.Outcome);
        Assert.Equal(DiscordApiOutcome.MissingToken, permissions.Failure?.Outcome);
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task ListingStillWorksWhenTheBotPermissionsCannotBeRead()
    {
        _api.On(MemberRoute, () => FakeDiscordApi.Json(HttpStatusCode.Forbidden, """{"message":"Missing Access","code":50001}"""));

        var result = await CreateDirectory().GetChannelsAsync(GuildId, refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Count);
        Assert.All(result.Value, channel => Assert.Null(channel.CanPost));
    }

    [Fact]
    public async Task TimedOutBotCannotPostAnywhere()
    {
        _api.On(MemberRoute, MemberTimedOutUntil("2026-10-18T13:00:00+00:00"));
        var directory = CreateDirectory();

        var permissions = await directory.CheckPermissionsAsync(GuildId, GeneralId, refresh: false, Cancel);
        var channels = await directory.GetChannelsAsync(GuildId, refresh: false, Cancel);

        Assert.False(permissions.Value!.CanPost);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 13, 0, 0, TimeSpan.Zero), permissions.Value.TimedOutUntil);
        Assert.Equal(["sendMessages", "embedLinks"], permissions.Value.Permissions.Where(check => !check.Granted).Select(check => check.Name));
        Assert.All(channels.Value!, channel => Assert.False(channel.CanPost));
    }

    [Fact]
    public async Task ExpiredTimeoutIsIgnored()
    {
        _api.On(MemberRoute, MemberTimedOutUntil("2026-10-18T11:00:00+00:00"));

        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, GeneralId, refresh: false, Cancel);

        Assert.True(result.Value!.CanPost);
        Assert.Null(result.Value.TimedOutUntil);
    }

    [Fact]
    public async Task TimeoutDoesNotApplyToAdministrators()
    {
        _api.On(GuildRoute, Guild(botRole: DiscordPermissions.Administrator))
            .On(MemberRoute, MemberTimedOutUntil("2026-10-18T13:00:00+00:00", BotRoleId));

        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, GeneralId, refresh: false, Cancel);

        Assert.True(result.Value!.CanPost);
    }

    [Fact]
    public async Task TextChannelWithEveryPermissionCanPost()
    {
        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, GeneralId, refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.Equal(GuildId, result.Value.GuildId);
        Assert.Equal(GeneralId, result.Value.ChannelId);
        Assert.Equal("text", result.Value.ChannelType);
        Assert.True(result.Value.CanPost);
        Assert.Equal(
            [
                new DiscordPermissionCheck("viewChannel", "View Channel", true),
                new DiscordPermissionCheck("sendMessages", "Send Messages", true),
                new DiscordPermissionCheck("embedLinks", "Embed Links", true),
            ],
            result.Value.Permissions);
        Assert.Contains(MemberRoute, _api.RequestedRoutes);
    }

    [Fact]
    public async Task OverwriteDenyingEmbedLinksIsReported()
    {
        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, NewsId, refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.Equal("announcement", result.Value.ChannelType);
        Assert.False(result.Value.CanPost);
        Assert.Equal(["embedLinks"], result.Value.Permissions.Where(check => !check.Granted).Select(check => check.Name));
    }

    [Fact]
    public async Task HiddenChannelGrantsNothing()
    {
        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, HiddenId, refresh: false, Cancel);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Permissions, check => Assert.False(check.Granted));
    }

    [Fact]
    public async Task OwnerCanPostEverywhere()
    {
        _api.On(GuildRoute, Guild(ownerId: BotId, everyone: 0));

        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, HiddenId, refresh: false, Cancel);

        Assert.True(result.Value!.CanPost);
    }

    [Fact]
    public async Task AdministratorRoleCanPostEverywhere()
    {
        _api.On(GuildRoute, Guild(everyone: 0, botRole: DiscordPermissions.Administrator)).On(MemberRoute, Member(BotRoleId));

        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, HiddenId, refresh: false, Cancel);

        Assert.True(result.Value!.CanPost);
    }

    [Theory]
    [InlineData("999")]
    [InlineData(VoiceId)]
    [InlineData(CategoryId)]
    [InlineData(ForumId)]
    public async Task UnknownOrUnsupportedChannelIsNotFound(string channelId)
    {
        var result = await CreateDirectory().CheckPermissionsAsync(GuildId, channelId, refresh: false, Cancel);

        Assert.Equal(404, result.Failure?.StatusCode);
        Assert.DoesNotContain(MemberRoute, _api.RequestedRoutes);
    }

    [Fact]
    public async Task PermissionChecksReuseCachedData()
    {
        var directory = CreateDirectory();

        await directory.GetChannelsAsync(GuildId, refresh: false, Cancel);
        await directory.CheckPermissionsAsync(GuildId, GeneralId, refresh: false, Cancel);
        await directory.CheckPermissionsAsync(GuildId, NewsId, refresh: false, Cancel);

        Assert.Equal([ChannelsRoute, UserRoute, GuildRoute, MemberRoute], _api.RequestedRoutes);
    }
}
