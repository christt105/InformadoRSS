using System.Net;
using System.Text;
using InformadoRSS.Core;
using InformadoRSS.Sources;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace InformadoRSS.Tests.Sources;

public sealed class RssAtomSourceTests
{
    private const string MinimalFeed = """
        <rss version="2.0"><channel><title>Feed</title>
          <item><guid>1</guid><title>First</title><link>https://example.org/1</link></item>
        </channel></rss>
        """;

    private static readonly Uri FeedUrl = new("https://example.org/feed.xml");

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return respond(request, cancellationToken);
        }
    }

    private static (RssAtomSource Source, FakeHandler Handler) CreateSource(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        RssAtomSourceOptions? options = null)
    {
        var handler = new FakeHandler(respond);
        return (new RssAtomSource(new HttpClient(handler), options ?? new RssAtomSourceOptions()), handler);
    }

    private static (RssAtomSource Source, FakeHandler Handler) CreateSource(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        CreateSource((request, _) => Task.FromResult(respond(request)));

    private static HttpResponseMessage Xml(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/rss+xml") };

    [Fact]
    public async Task SuccessfulFetchReturnsItemsAndValidators()
    {
        var (source, handler) = CreateSource(_ =>
        {
            var response = Xml(MinimalFeed);
            response.Headers.TryAddWithoutValidation("ETag", "W/\"abc\"");
            response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Sat, 17 Oct 2026 10:00:00 GMT");
            return response;
        });

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        var fetched = Assert.IsType<FeedFetched>(result);
        Assert.Equal("Feed", fetched.Title);
        Assert.Equal("First", Assert.Single(fetched.Items).Title);
        Assert.Equal(new CacheValidators("W/\"abc\"", "Sat, 17 Oct 2026 10:00:00 GMT"), fetched.Validators);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(FeedUrl, request.RequestUri);
        Assert.Equal(RssAtomSource.UserAgent, request.Headers.UserAgent.ToString());
        Assert.StartsWith("InformadoRSS/", RssAtomSource.UserAgent, StringComparison.Ordinal);
        Assert.EndsWith("(+https://github.com/christt105/InformadoRSS)", RssAtomSource.UserAgent, StringComparison.Ordinal);
        Assert.False(request.Headers.Contains("If-None-Match"));
        Assert.False(request.Headers.Contains("If-Modified-Since"));
    }

    [Fact]
    public async Task SendsStoredValidatorsVerbatim()
    {
        var (source, handler) = CreateSource(_ => Xml(MinimalFeed));
        var validators = new CacheValidators("\"v1\"", "Sat, 17 Oct 2026 10:00:00 GMT");

        await source.FetchAsync(new FeedDefinition(FeedUrl, validators), TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(["\"v1\""], request.Headers.GetValues("If-None-Match"));
        Assert.Equal(["Sat, 17 Oct 2026 10:00:00 GMT"], request.Headers.GetValues("If-Modified-Since"));
    }

    [Fact]
    public async Task NotModifiedKeepsPreviousValidatorsWhenServerSendsNone()
    {
        var (source, _) = CreateSource(_ => new HttpResponseMessage(HttpStatusCode.NotModified));
        var validators = new CacheValidators("\"v1\"", null);

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl, validators), TestContext.Current.CancellationToken);

        Assert.Equal(new FeedNotModified(validators), result);
    }

    [Fact]
    public async Task NotModifiedReturnsNewValidatorsWhenServerSendsThem()
    {
        var (source, _) = CreateSource(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.TryAddWithoutValidation("ETag", "\"v2\"");
            return response;
        });

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl, new CacheValidators("\"v1\"", null)), TestContext.Current.CancellationToken);

        Assert.Equal(new FeedNotModified(new CacheValidators("\"v2\"", null)), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ErrorStatusIsReported(HttpStatusCode status)
    {
        var (source, _) = CreateSource(_ => Xml("<html/>", status));

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        var error = Assert.IsType<FeedFetchFailed>(result).Error;
        Assert.Equal(FetchErrorKind.HttpStatus, error.Kind);
        Assert.Equal(status, error.StatusCode);
        Assert.Null(error.RetryAfter);
    }

    [Fact]
    public async Task RetryAfterIsReported()
    {
        var (source, _) = CreateSource(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "120");
            return response;
        });

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        var error = Assert.IsType<FeedFetchFailed>(result).Error;
        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(120), error.RetryAfter);
    }

    [Fact]
    public async Task TimeoutIsReported()
    {
        var (source, _) = CreateSource(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Xml(MinimalFeed);
            },
            new RssAtomSourceOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        Assert.Equal(FetchErrorKind.Timeout, Assert.IsType<FeedFetchFailed>(result).Error.Kind);
    }

    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        var (source, _) = CreateSource(async (_, cancellationToken) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Xml(MinimalFeed);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.FetchAsync(new FeedDefinition(FeedUrl), cts.Token));
    }

    [Fact]
    public async Task NetworkErrorIsReported()
    {
        var (source, _) = CreateSource(_ => throw new HttpRequestException("Name or service not known"));

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        var error = Assert.IsType<FeedFetchFailed>(result).Error;
        Assert.Equal(FetchErrorKind.Network, error.Kind);
        Assert.Contains("Name or service not known", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnparsableBodyIsReportedAndDropsValidators()
    {
        var (source, _) = CreateSource(_ =>
        {
            var response = Xml("<!doctype html><html><body>Cloudflare</body></html>");
            response.Headers.TryAddWithoutValidation("ETag", "\"x\"");
            return response;
        });

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        Assert.Equal(FetchErrorKind.Parse, Assert.IsType<FeedFetchFailed>(result).Error.Kind);
    }

    [Fact]
    public async Task OversizedBodyIsRejected()
    {
        var (source, _) = CreateSource(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(new byte[4096])) }),
            new RssAtomSourceOptions { MaxResponseBytes = 1024 });

        var result = await source.FetchAsync(new FeedDefinition(FeedUrl), TestContext.Current.CancellationToken);

        Assert.Equal(FetchErrorKind.TooLarge, Assert.IsType<FeedFetchFailed>(result).Error.Kind);
    }

    [Fact]
    public void IsRegisteredAsSourceInTheApplication()
    {
        using var factory = new WebApplicationFactory<Program>();

        var source = factory.Services.GetRequiredService<ISource>();

        Assert.IsType<RssAtomSource>(source);
    }
}
