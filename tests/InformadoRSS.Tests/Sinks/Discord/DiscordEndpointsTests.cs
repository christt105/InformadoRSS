using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InformadoRSS.Sinks.Discord;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static InformadoRSS.Tests.Sinks.Discord.DiscordFixtures;

namespace InformadoRSS.Tests.Sinks.Discord;

public sealed class DiscordEndpointsTests : IAsyncDisposable
{
    private const string Token = "endpoint-token";

    private readonly AppFactory _factory = new();
    private readonly FakeDiscordApi _api = new();

    public DiscordEndpointsTests()
    {
        _api.On("GET users/@me", CurrentUser)
            .On("GET users/@me/guilds?limit=200", Guilds)
            .On($"GET guilds/{GuildId}/channels", Channels)
            .On($"GET guilds/{GuildId}", Guild())
            .On($"GET guilds/{GuildId}/members/{BotId}", Member())
            .On($"POST channels/{GeneralId}/messages", """{"id":"5000","channel_id":"301"}""");
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private HttpClient CreateClient(string? token = Token)
    {
        var app = _factory.WithWebHostBuilder(builder =>
        {
            if (token is not null)
            {
                builder.UseSetting(SettingsDiscordTokenProvider.ConfigurationKey, token);
            }

            builder.ConfigureTestServices(services =>
                services.AddHttpClient<DiscordRestClient>().ConfigurePrimaryHttpMessageHandler(() => _api));
        });
        return app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        return document.RootElement.Clone();
    }

    private static Uri Url(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task ListsGuilds()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(Url("/api/discord/guilds"), Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var guilds = (await ReadJsonAsync(response)).EnumerateArray().ToList();
        Assert.Equal(["animated", "los panas", "Zanahorios"], guilds.Select(guild => guild.GetProperty("name").GetString()));
        Assert.Equal("100", guilds[1].GetProperty("id").GetString());
        Assert.Equal("https://cdn.discordapp.com/icons/100/abc.png", guilds[1].GetProperty("iconUrl").GetString());
        Assert.Equal($"Bot {Token}", _api.Requests[0].Request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task ListsChannelsAndUsesTheCacheUnlessRefreshIsAsked()
    {
        using var client = CreateClient();

        using var first = await client.GetAsync(Url($"/api/discord/guilds/{GuildId}/channels"), Cancel);
        using var cached = await client.GetAsync(Url($"/api/discord/guilds/{GuildId}/channels"), Cancel);
        using var refreshed = await client.GetAsync(Url($"/api/discord/guilds/{GuildId}/channels?refresh=true"), Cancel);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var channels = (await ReadJsonAsync(first)).EnumerateArray().ToList();
        Assert.Equal([GeneralId, NewsId, ForumId, HiddenId, AlertsId], channels.Select(channel => channel.GetProperty("id").GetString()));
        Assert.Equal("forum", channels[2].GetProperty("type").GetString());
        Assert.Equal("Feeds", channels[4].GetProperty("categoryName").GetString());
        Assert.Equal(CategoryId, channels[4].GetProperty("categoryId").GetString());
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task ReportsChannelPermissions()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(Url($"/api/discord/guilds/{GuildId}/channels/{NewsId}/permissions"), Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(NewsId, body.GetProperty("channelId").GetString());
        Assert.Equal("announcement", body.GetProperty("channelType").GetString());
        Assert.False(body.GetProperty("canPost").GetBoolean());
        var embedLinks = body.GetProperty("permissions").EnumerateArray().Single(check => check.GetProperty("name").GetString() == "embedLinks");
        Assert.Equal("Embed Links", embedLinks.GetProperty("label").GetString());
        Assert.False(embedLinks.GetProperty("granted").GetBoolean());
    }

    [Fact]
    public async Task SendsTestMessageWithTheDefaultTemplate()
    {
        using var client = CreateClient();

        using var response = await client.PostAsync(Url($"/api/discord/channels/{GeneralId}/test-message"), null, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(GeneralId, body.GetProperty("channelId").GetString());
        Assert.Equal("5000", body.GetProperty("messageId").GetString());
        using var sent = JsonDocument.Parse(Assert.Single(_api.Requests).Body);
        Assert.Equal($"**{DiscordEndpoints.TestItemTitle}**\n{DiscordEndpoints.TestItemLink}", sent.RootElement.GetProperty("content").GetString());
        Assert.Equal(0, sent.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
    }

    [Fact]
    public async Task SendsTestMessageWithTheGivenTemplate()
    {
        using var client = CreateClient();
        var request = new DiscordTestMessageRequest("""{"embed":{"title":"{{title}}","url":"{{link}}","color":5793266}}""");

        using var response = await client.PostAsJsonAsync(Url($"/api/discord/channels/{GeneralId}/test-message"), request, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var sent = JsonDocument.Parse(Assert.Single(_api.Requests).Body);
        var embed = sent.RootElement.GetProperty("embeds")[0];
        Assert.Equal(DiscordEndpoints.TestItemTitle, embed.GetProperty("title").GetString());
        Assert.Equal(DiscordEndpoints.TestItemLink, embed.GetProperty("url").GetString());
        Assert.Equal(5793266, embed.GetProperty("color").GetInt32());
    }

    [Fact]
    public async Task InvalidTemplateIsRejectedBeforeSending()
    {
        using var client = CreateClient();
        var request = new DiscordTestMessageRequest("""{"content":"{{nope}}"}""");

        using var response = await client.PostAsJsonAsync(Url($"/api/discord/channels/{GeneralId}/test-message"), request, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_template", body.GetProperty("error").GetString());
        Assert.Equal("$.content", body.GetProperty("templateErrors")[0].GetProperty("path").GetString());
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task MalformedBodyIsRejectedBeforeSending()
    {
        using var client = CreateClient();
        using var content = new StringContent("{\"templateJson\":", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Url($"/api/discord/channels/{GeneralId}/test-message"), content, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_body", (await ReadJsonAsync(response)).GetProperty("error").GetString());
        Assert.Empty(_api.Requests);
    }

    [Theory]
    [InlineData("/api/discord/guilds/abc/channels")]
    [InlineData("/api/discord/guilds/100/channels/general/permissions")]
    public async Task NonNumericIdsAreRejected(string path)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(Url(path), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_id", (await ReadJsonAsync(response)).GetProperty("error").GetString());
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task MissingTokenIsAConflict()
    {
        using var client = CreateClient(token: null);

        using var guilds = await client.GetAsync(Url("/api/discord/guilds"), Cancel);
        using var test = await client.PostAsync(Url($"/api/discord/channels/{GeneralId}/test-message"), null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, guilds.StatusCode);
        Assert.Equal("token_missing", (await ReadJsonAsync(guilds)).GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.Conflict, test.StatusCode);
        Assert.Empty(_api.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"401: Unauthorized","code":0}""", HttpStatusCode.BadGateway, "token_invalid")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"Missing Access","code":50001}""", HttpStatusCode.Forbidden, "forbidden")]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Unknown Guild","code":10004}""", HttpStatusCode.NotFound, "not_found")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", HttpStatusCode.BadGateway, "discord_unavailable")]
    [InlineData(HttpStatusCode.BadRequest, """{"message":"Invalid Form Body","code":50035}""", HttpStatusCode.BadGateway, "discord_error")]
    public async Task MapsDiscordErrors(HttpStatusCode discordStatus, string discordBody, HttpStatusCode expectedStatus, string expectedError)
    {
        _api.On($"GET guilds/{GuildId}/channels", () => FakeDiscordApi.Json(discordStatus, discordBody));
        using var client = CreateClient();

        using var response = await client.GetAsync(Url($"/api/discord/guilds/{GuildId}/channels"), Cancel);

        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(expectedError, body.GetProperty("error").GetString());
        Assert.DoesNotContain(Token, body.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForbiddenTestMessageExplainsDiscordsError()
    {
        _api.On(
            $"POST channels/{GeneralId}/messages",
            () => FakeDiscordApi.Json(HttpStatusCode.Forbidden, """{"message":"Missing Permissions","code":50013}"""));
        using var client = CreateClient();

        using var response = await client.PostAsync(Url($"/api/discord/channels/{GeneralId}/test-message"), null, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var message = (await ReadJsonAsync(response)).GetProperty("message").GetString();
        Assert.Contains("code 50013", message, StringComparison.Ordinal);
        Assert.Contains("Missing Permissions", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitReturnsRetryAfter()
    {
        _api.On(
            "GET users/@me/guilds?limit=200",
            () => FakeDiscordApi.Json(HttpStatusCode.TooManyRequests, """{"message":"You are being rate limited.","retry_after":120,"global":false}"""));
        using var client = CreateClient();

        using var response = await client.GetAsync(Url("/api/discord/guilds"), Cancel);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("rate_limited", body.GetProperty("error").GetString());
        Assert.Equal(120, body.GetProperty("retryAfterSeconds").GetDouble());
    }
}
