using System.Net;
using System.Text;
using InformadoRSS.Core;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Tests.Storage;

namespace InformadoRSS.Tests.Sinks.Ntfy;

public sealed class NtfySinkTests : IAsyncLifetime
{
    private readonly InMemoryDatabase _db = new();
    private readonly FakeNtfyServer _server = new();
    private NtfySink _sink = null!;

    public async ValueTask InitializeAsync()
    {
        await _db.InitializeAsync();
        await _db.Settings.SetAsync(NtfySettings.UrlKey, "http://192.168.1.15:8094/", TestContext.Current.CancellationToken);
        _sink = new NtfySink(new NtfyClient(new HttpClient(_server), _db.Settings));
    }

    public async ValueTask DisposeAsync()
    {
        _server.Dispose();
        await _db.DisposeAsync();
    }

    private static Destination Topic(string topic) =>
        new(NtfySink.SinkName, new Dictionary<string, string> { [NtfySink.TopicParameter] = topic });

    private Task<DeliveryResult> Deliver(RenderedMessage message, string topic = "servidor") =>
        _sink.DeliverAsync(Topic(topic), message, TestContext.Current.CancellationToken);

    [Fact]
    public async Task PostsPlainTextToTopicUrl()
    {
        var result = await Deliver(new RenderedMessage("**Hello**\nhttps://example.org"));

        Assert.Equal(DeliveryResult.Delivered, result);
        var (request, body) = Assert.Single(_server.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://192.168.1.15:8094/servidor", request.RequestUri!.AbsoluteUri);
        Assert.Equal("**Hello**\nhttps://example.org", body);
        Assert.Equal("text/plain", request.Content!.Headers.ContentType!.MediaType);
        Assert.Null(request.Headers.Authorization);
        Assert.Null(FakeNtfyServer.Header(request, "Title"));
    }

    [Fact]
    public async Task EmbedProvidesTitleClickAndFallbackBody()
    {
        var message = new RenderedMessage(
            string.Empty, new RenderedEmbed(Title: "Tetris", Description: "A movie", Url: "https://example.org/tetris"));

        await Deliver(message);

        var (request, body) = Assert.Single(_server.Requests);
        Assert.Equal("A movie", body);
        Assert.Equal("Tetris", FakeNtfyServer.Header(request, "Title"));
        Assert.Equal("https://example.org/tetris", FakeNtfyServer.Header(request, "Click"));
    }

    [Fact]
    public async Task NonAsciiHeadersAreRfc2047Encoded()
    {
        await Deliver(new RenderedMessage("body", new RenderedEmbed(Title: "Edición\nDeluxe")));

        var title = FakeNtfyServer.Header(Assert.Single(_server.Requests).Request, "Title");
        Assert.Equal($"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes("Edición Deluxe"))}?=", title);
    }

    [Theory]
    [InlineData("tk_abcdef", "Bearer", "tk_abcdef")]
    [InlineData("christian:secret", "Basic", "Y2hyaXN0aWFuOnNlY3JldA==")]
    public async Task AccessTokenIsSent(string token, string scheme, string parameter)
    {
        await _db.Settings.SetAsync(NtfySettings.TokenKey, token, TestContext.Current.CancellationToken);

        await Deliver(new RenderedMessage("hi"));

        var authorization = Assert.Single(_server.Requests).Request.Headers.Authorization;
        Assert.Equal(scheme, authorization!.Scheme);
        Assert.Equal(parameter, authorization.Parameter);
    }

    [Fact]
    public async Task MissingServerUrlIsTemporary()
    {
        await _db.Settings.SetAsync(NtfySettings.UrlKey, null, TestContext.Current.CancellationToken);

        var result = await Deliver(new RenderedMessage("hi"));

        Assert.Equal(DeliveryStatus.Transient, result.Status);
        Assert.Empty(_server.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("a/b")]
    public async Task InvalidTopicIsPermanent(string topic)
    {
        var result = await Deliver(new RenderedMessage("hi"), topic);

        Assert.Equal(DeliveryStatus.Permanent, result.Status);
        Assert.Empty(_server.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, DeliveryStatus.Permanent)]
    [InlineData(HttpStatusCode.Unauthorized, DeliveryStatus.Transient)]
    [InlineData(HttpStatusCode.NotFound, DeliveryStatus.Transient)]
    [InlineData(HttpStatusCode.TooManyRequests, DeliveryStatus.Transient)]
    [InlineData(HttpStatusCode.BadGateway, DeliveryStatus.Transient)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, DeliveryStatus.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, DeliveryStatus.Rejected)]
    public async Task ErrorStatusesAreClassified(HttpStatusCode status, DeliveryStatus expected)
    {
        _server.Respond = _ => FakeNtfyServer.Error(status, """{"code":40301,"http":403,"error":"forbidden"}""");

        var result = await Deliver(new RenderedMessage("hi"));

        Assert.Equal(expected, result.Status);
        Assert.Contains("forbidden", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkErrorIsTemporary()
    {
        _server.Respond = _ => throw new HttpRequestException("connection refused");

        var result = await Deliver(new RenderedMessage("hi"));

        Assert.Equal(DeliveryStatus.Transient, result.Status);
        Assert.Contains("connection refused", result.Error, StringComparison.Ordinal);
    }
}
