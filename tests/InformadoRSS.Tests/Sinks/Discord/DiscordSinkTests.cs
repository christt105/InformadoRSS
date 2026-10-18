using System.Net;
using System.Text;
using System.Text.Json;
using InformadoRSS.Core;
using InformadoRSS.Sinks.Discord;

namespace InformadoRSS.Tests.Sinks.Discord;

public sealed class DiscordSinkTests
{
    private const string Token = "test-token";
    private const string ChannelId = "123456789012345678";

    private static readonly RenderedMessage TextMessage = new("**Hello** @everyone\nhttps://example.org");

    private sealed class FakeHandler(Func<int, HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return respond(Requests.Count - 1, request);
        }
    }

    private sealed class FixedTokenProvider(string? token) : IDiscordTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult(token);
    }

    private static (DiscordSink Sink, FakeHandler Handler) CreateSink(
        Func<int, HttpRequestMessage, HttpResponseMessage> respond,
        string? token = Token,
        DiscordClientOptions? options = null)
    {
        var handler = new FakeHandler(respond);
        var client = new DiscordRestClient(
            new HttpClient(handler),
            new FixedTokenProvider(token),
            new DiscordRateLimiter(TimeProvider.System),
            options ?? new DiscordClientOptions(),
            TimeProvider.System);
        return (new DiscordSink(client), handler);
    }

    private static Destination Channel(string channelId = ChannelId) =>
        new(DiscordSink.SinkName, new Dictionary<string, string> { ["guildId"] = "1", ["channelId"] = channelId });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Ok() => Json(HttpStatusCode.OK, """{"id":"1"}""");

    private static HttpResponseMessage TooManyRequests(double retryAfter, bool global = false) =>
        Json(
            HttpStatusCode.TooManyRequests,
            $$"""{"message":"You are being rate limited.","retry_after":{{retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"global":{{(global ? "true" : "false")}}}""");

    private static Task<DeliveryResult> Deliver(DiscordSink sink, Destination? destination = null, RenderedMessage? message = null) =>
        sink.DeliverAsync(destination ?? Channel(), message ?? TextMessage, TestContext.Current.CancellationToken);

    [Fact]
    public async Task PostsMessageWithEmbedAndNoMentions()
    {
        var (sink, handler) = CreateSink((_, _) => Ok());
        var message = new RenderedMessage(
            string.Empty,
            new RenderedEmbed(
                Title: "Tetris",
                Url: "https://example.org/tetris",
                Color: 0x5865F2,
                ImageUrl: "https://example.org/cover.webp",
                Footer: "movie",
                Timestamp: new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero)));

        var result = await Deliver(sink, message: message);

        Assert.Equal(DeliveryResult.Delivered, result);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri($"https://discord.com/api/v10/channels/{ChannelId}/messages"), request.RequestUri);
        Assert.Equal($"Bot {Token}", request.Headers.Authorization?.ToString());
        Assert.StartsWith("DiscordBot (https://github.com/christt105/InformadoRSS, ", string.Join(' ', request.Headers.GetValues("User-Agent")), StringComparison.Ordinal);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.False(root.TryGetProperty("content", out _));
        Assert.Equal(0, root.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
        var embed = Assert.Single(root.GetProperty("embeds").EnumerateArray().ToList());
        Assert.Equal("Tetris", embed.GetProperty("title").GetString());
        Assert.Equal("https://example.org/tetris", embed.GetProperty("url").GetString());
        Assert.Equal(0x5865F2, embed.GetProperty("color").GetInt32());
        Assert.Equal("https://example.org/cover.webp", embed.GetProperty("image").GetProperty("url").GetString());
        Assert.Equal("movie", embed.GetProperty("footer").GetProperty("text").GetString());
        Assert.Equal("2026-10-12T00:00:00+00:00", embed.GetProperty("timestamp").GetString());
        Assert.False(embed.TryGetProperty("description", out _));
        Assert.False(embed.TryGetProperty("thumbnail", out _));
    }

    [Fact]
    public async Task PostsPlainContentWithoutEmbeds()
    {
        var (sink, handler) = CreateSink((_, _) => Ok());

        await Deliver(sink);

        using var json = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(TextMessage.Content, json.RootElement.GetProperty("content").GetString());
        Assert.False(json.RootElement.TryGetProperty("embeds", out _));
    }

    [Fact]
    public async Task RetriesAfter429()
    {
        var (sink, handler) = CreateSink((index, _) => index == 0 ? TooManyRequests(0.01) : Ok());

        var result = await Deliver(sink);

        Assert.Equal(DeliveryStatus.Delivered, result.Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GivesUpAfterMaxRetries()
    {
        var (sink, handler) = CreateSink((_, _) => TooManyRequests(0.01), options: new DiscordClientOptions { MaxRetries = 2 });

        var result = await Deliver(sink);

        Assert.Equal(DeliveryStatus.Transient, result.Status);
        Assert.NotNull(result.RetryAfter);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task LongRetryAfterIsReturnedInsteadOfWaiting()
    {
        var (sink, handler) = CreateSink((_, _) => TooManyRequests(120));

        var result = await Deliver(sink);

        Assert.Equal(DeliveryStatus.Transient, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(120), result.RetryAfter);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ExhaustedBucketBlocksOnlyThatChannel()
    {
        var (sink, handler) = CreateSink((_, _) =>
        {
            var response = Ok();
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset-After", "60.5");
            return response;
        });

        Assert.Equal(DeliveryStatus.Delivered, (await Deliver(sink)).Status);
        var blocked = await Deliver(sink);
        var other = await Deliver(sink, Channel("42"));

        Assert.Equal(DeliveryStatus.Transient, blocked.Status);
        Assert.InRange(blocked.RetryAfter!.Value, TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(61));
        Assert.Equal(DeliveryStatus.Delivered, other.Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Global429BlocksEveryChannel()
    {
        var (sink, handler) = CreateSink((_, _) => TooManyRequests(60, global: true));

        await Deliver(sink);
        var other = await Deliver(sink, Channel("42"));

        Assert.Equal(DeliveryStatus.Transient, other.Status);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"401: Unauthorized","code":0}""", DeliveryStatus.Transient, "bot token")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"Missing Access","code":50001}""", DeliveryStatus.Permanent, "code 50001")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"Missing Permissions","code":50013}""", DeliveryStatus.Permanent, "Missing Permissions")]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Unknown Channel","code":10003}""", DeliveryStatus.Permanent, "Unknown Channel")]
    [InlineData(HttpStatusCode.BadRequest, """{"message":"Invalid Form Body","code":50035}""", DeliveryStatus.Permanent, "rejected the message")]
    [InlineData(HttpStatusCode.InternalServerError, "", DeliveryStatus.Transient, "HTTP 500")]
    [InlineData(HttpStatusCode.BadGateway, "<html>bad gateway</html>", DeliveryStatus.Transient, "HTTP 502")]
    public async Task MapsErrorResponses(HttpStatusCode status, string body, DeliveryStatus expected, string errorPart)
    {
        var (sink, _) = CreateSink((_, _) => Json(status, body));

        var result = await Deliver(sink);

        Assert.Equal(expected, result.Status);
        Assert.Contains(errorPart, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkErrorIsTransient()
    {
        var (sink, _) = CreateSink((_, _) => throw new HttpRequestException("connection refused"));

        var result = await Deliver(sink);

        Assert.Equal(DeliveryStatus.Transient, result.Status);
        Assert.Contains("connection refused", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingTokenIsTransientAndSendsNothing()
    {
        var (sink, handler) = CreateSink((_, _) => Ok(), token: null);

        var result = await Deliver(sink);

        Assert.Equal(DeliveryResult.Transient("Discord bot token not configured"), result);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("general")]
    public async Task InvalidChannelIdIsPermanent(string? channelId)
    {
        var (sink, handler) = CreateSink((_, _) => Ok());
        var parameters = channelId is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["channelId"] = channelId };

        var result = await Deliver(sink, new Destination(DiscordSink.SinkName, parameters));

        Assert.Equal(DeliveryStatus.Permanent, result.Status);
        Assert.Empty(handler.Requests);
    }
}
