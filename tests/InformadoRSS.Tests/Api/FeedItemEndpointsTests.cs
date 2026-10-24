using System.Net;
using InformadoRSS.Core;
using InformadoRSS.Storage;
using InformadoRSS.Tests.Scheduling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static InformadoRSS.Tests.Api.ApiTestClient;

namespace InformadoRSS.Tests.Api;

public sealed class FeedItemEndpointsTests : IAsyncDisposable
{
    private readonly AppFactory _factory = new();
    private readonly FakeSource _source = new();
    private readonly FakeSink _discord = new("discord");
    private readonly HttpClient _http;
    private readonly IServiceProvider _services;

    public FeedItemEndpointsTests()
    {
        var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISource>().AddSingleton<ISource>(_source);
                services.RemoveAll<ISink>().AddSingleton<ISink>(_discord);
            }));
        _http = app.CreateClient();
        _services = app.Services;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<(long FeedId, long RouteId)> CreateFeedWithRouteAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var feeds = _services.GetRequiredService<IFeedStore>();
        var feed = await feeds.AddAsync(new FeedSettings(new Uri("https://example.com/blog.xml"), "Blog", TimeSpan.FromMinutes(10)), ct);
        var route = await _services.GetRequiredService<IRouteStore>()
            .AddAsync(feed.Id, new RouteSettings("Posts", null, null, true, []), ct);
        return (feed.Id, route!.Id);
    }

    private Task<HttpResponseMessage> PostAsync(string path, string json) =>
        _http.PostAsync(path, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

    [Fact]
    public async Task ItemsAreListedNewestFirstWithAgeAndRouteState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (feedId, routeId) = await CreateFeedWithRouteAsync();
        await _services.GetRequiredService<ISeenItemStore>().MarkSeenAsync(routeId, ["old"], DateTimeOffset.UtcNow, ct);
        var now = DateTimeOffset.UtcNow;
        _source.ReturnsItems(
            PipelineHarness.Item("new", now.AddHours(-1)), PipelineHarness.Item("old", now.AddDays(-30)));

        using var response = await _http.GetAsync($"/api/feeds/{feedId}/items", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(7 * 24 * 3600, body.GetProperty("maxItemAgeSeconds").GetInt64());
        var items = body.GetProperty("items");
        Assert.Equal("new", items[0].GetProperty("id").GetString());
        Assert.False(items[0].GetProperty("tooOld").GetBoolean());
        Assert.False(items[0].GetProperty("routes")[0].GetProperty("seen").GetBoolean());
        Assert.Equal("old", items[1].GetProperty("id").GetString());
        Assert.True(items[1].GetProperty("tooOld").GetBoolean());
        var state = items[1].GetProperty("routes")[0];
        Assert.Equal(routeId, state.GetProperty("routeId").GetInt64());
        Assert.True(state.GetProperty("seen").GetBoolean());
    }

    [Fact]
    public async Task ListFailsWhenTheFeedCannotBeFetched()
    {
        var (feedId, _) = await CreateFeedWithRouteAsync();
        _source.Returns(new FeedFetchFailed(new FetchError(FetchErrorKind.Network, "boom")));

        using var response = await _http.GetAsync($"/api/feeds/{feedId}/items", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fetch_failed", (await ReadAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task ResetMakesTheRouteForgetTheItem()
    {
        var ct = TestContext.Current.CancellationToken;
        var (feedId, routeId) = await CreateFeedWithRouteAsync();
        var seen = _services.GetRequiredService<ISeenItemStore>();
        await seen.MarkSeenAsync(routeId, ["a"], DateTimeOffset.UtcNow, ct);

        using var response = await PostAsync($"/api/feeds/{feedId}/items/reset", """{"itemId":"a"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await ReadAsync(response)).GetProperty("reset").GetInt32());
        Assert.Equal(["a"], await seen.FilterUnseenAsync(routeId, ["a"], ct));

        using var again = await PostAsync($"/api/feeds/{feedId}/items/reset", """{"itemId":"a"}""");
        Assert.Equal(0, (await ReadAsync(again)).GetProperty("reset").GetInt32());
    }

    [Theory]
    [InlineData("""{"routeId":1}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"itemId":"a","routeId":999}""", HttpStatusCode.NotFound)]
    public async Task InvalidResetIsRejected(string body, HttpStatusCode expected)
    {
        var (feedId, _) = await CreateFeedWithRouteAsync();

        using var response = await PostAsync($"/api/feeds/{feedId}/items/reset", body);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SendDeliversToTheChosenChannelWithoutTouchingState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (feedId, routeId) = await CreateFeedWithRouteAsync();
        await _services.GetRequiredService<ISeenItemStore>().MarkSeenAsync(routeId, ["a"], DateTimeOffset.UtcNow, ct);
        _source.ReturnsItems(PipelineHarness.Item("a", DateTimeOffset.UtcNow.AddDays(-30)));

        using var response = await PostAsync(
            $"/api/feeds/{feedId}/items/send", $$"""{"itemId":"a","routeId":{{routeId}},"channelId":"123456789012345678"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ReadAsync(response)).GetProperty("sent").GetBoolean());
        var attempt = Assert.Single(_discord.Attempts);
        Assert.Equal("123456789012345678", attempt.Destination.Parameters["channelId"]);
        Assert.Empty(await _services.GetRequiredService<ISeenItemStore>().FilterUnseenAsync(routeId, ["a"], ct));
    }

    [Fact]
    public async Task SendReportsDestinationsThatRejectedTheMessage()
    {
        var (feedId, routeId) = await CreateFeedWithRouteAsync();
        _source.ReturnsItems(PipelineHarness.Item("a"));
        _discord.Handler = (_, _) => DeliveryResult.Permanent("Unknown Channel");

        using var response = await PostAsync(
            $"/api/feeds/{feedId}/items/send", $$"""{"itemId":"a","routeId":{{routeId}},"channelId":"123456789012345678"}""");

        var body = await ReadAsync(response);
        Assert.False(body.GetProperty("sent").GetBoolean());
        Assert.Contains("Unknown Channel", body.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task SendWithoutChannelUsesTheRouteDestinations()
    {
        var ct = TestContext.Current.CancellationToken;
        var feeds = _services.GetRequiredService<IFeedStore>();
        var feed = await feeds.AddAsync(new FeedSettings(new Uri("https://example.com/b.xml"), "B", TimeSpan.FromMinutes(10)), ct);
        var route = await _services.GetRequiredService<IRouteStore>().AddAsync(
            feed.Id, new RouteSettings("R", null, null, true, [PipelineHarness.DiscordChannel("555")]), ct);
        _source.ReturnsItems(PipelineHarness.Item("a"));

        using var response = await PostAsync($"/api/feeds/{feed.Id}/items/send", $$"""{"itemId":"a","routeId":{{route!.Id}}}""");

        Assert.True((await ReadAsync(response)).GetProperty("sent").GetBoolean());
        Assert.Equal("555", Assert.Single(_discord.Attempts).Destination.Parameters["channelId"]);
    }

    [Theory]
    [InlineData("""{"routeId":1}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"itemId":"a"}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"itemId":"a","routeId":1,"channelId":"general"}""", HttpStatusCode.BadRequest)]
    [InlineData("""{"itemId":"missing","routeId":1}""", HttpStatusCode.NotFound)]
    public async Task InvalidSendIsRejected(string body, HttpStatusCode expected)
    {
        var (feedId, _) = await CreateFeedWithRouteAsync();
        _source.ReturnsItems(PipelineHarness.Item("a"));

        using var response = await PostAsync($"/api/feeds/{feedId}/items/send", body);

        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(_discord.Attempts);
    }

    [Fact]
    public async Task UnknownFeedIsNotFound()
    {
        using var response = await _http.GetAsync("/api/feeds/999/items", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
