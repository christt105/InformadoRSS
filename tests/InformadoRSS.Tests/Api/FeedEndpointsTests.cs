using System.Net;
using InformadoRSS.Core;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection;
using static InformadoRSS.Tests.Api.ApiTestClient;

namespace InformadoRSS.Tests.Api;

public sealed class FeedEndpointsTests : IAsyncDisposable
{
    private readonly ApiTestClient _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task CreatedFeedIsListedWithHealthAndDefaults()
    {
        using var created = await _api.PostAsync("/api/feeds", """{"url":"https://example.com/blog.xml"}""");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var feed = await ReadAsync(created);
        Assert.Equal("example.com", feed.GetProperty("name").GetString());
        Assert.Equal(600, feed.GetProperty("intervalSeconds").GetInt64());

        using var list = await _api.GetAsync("/api/feeds");
        var listed = Assert.Single((await ReadAsync(list)).EnumerateArray());
        Assert.Equal("https://example.com/blog.xml", listed.GetProperty("url").GetString());
        Assert.Equal(0, listed.GetProperty("health").GetProperty("consecutiveFailures").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, listed.GetProperty("latestItem").ValueKind);
        Assert.Equal(0, listed.GetProperty("routes").GetArrayLength());
    }

    [Fact]
    public async Task ListIncludesLatestItemAndFailures()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await _api.CreateFeedAsync();
        var published = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        await _api.Services.GetRequiredService<ILatestItemStore>()
            .SetAsync(id, new LatestItem("a", "Hello", "https://example.com/a", published, published), ct);
        await _api.Services.GetRequiredService<IFeedStore>().RecordFailureAsync(id, published, "HTTP 500", ct);

        using var response = await _api.GetAsync($"/api/feeds/{id}");

        var feed = await ReadAsync(response);
        Assert.Equal("Hello", feed.GetProperty("latestItem").GetProperty("title").GetString());
        Assert.Equal(1, feed.GetProperty("health").GetProperty("consecutiveFailures").GetInt32());
        Assert.Equal("HTTP 500", feed.GetProperty("health").GetProperty("lastError").GetString());
    }

    [Theory]
    [InlineData("""{"url":"ftp://example.com/feed"}""", "$.url")]
    [InlineData("""{"url":""}""", "$.url")]
    [InlineData("""{"url":"https://example.com/feed","intervalSeconds":5}""", "$.intervalSeconds")]
    public async Task InvalidFeedIsRejected(string body, string path)
    {
        using var response = await _api.PostAsync("/api/feeds", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadAsync(response);
        Assert.Equal("invalid_feed", error.GetProperty("error").GetString());
        Assert.Equal(path, error.GetProperty("errors")[0].GetProperty("path").GetString());
    }

    [Fact]
    public async Task MalformedBodyIsRejected()
    {
        using var response = await _api.PostAsync("/api/feeds", "{\"url\":");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_body", (await ReadAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task DuplicateUrlIsAConflict()
    {
        await _api.CreateFeedAsync();

        using var response = await _api.PostAsync("/api/feeds", """{"url":"https://example.com/feed.xml"}""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("duplicate_feed", (await ReadAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task FeedCanBeUpdatedAndDeleted()
    {
        var id = await _api.CreateFeedAsync();

        using var updated = await _api.PutAsync(
            $"/api/feeds/{id}", """{"url":"https://example.com/other.xml","name":"Other","intervalSeconds":900}""");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var feed = await ReadAsync(updated);
        Assert.Equal("Other", feed.GetProperty("name").GetString());
        Assert.Equal(900, feed.GetProperty("intervalSeconds").GetInt64());

        using var deleted = await _api.DeleteAsync($"/api/feeds/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var missing = await _api.GetAsync($"/api/feeds/{id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not_found", (await ReadAsync(missing)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidateFetchesWithoutSaving()
    {
        _api.Source.Returns(new FeedFetched(
            "My blog",
            Enumerable.Range(1, 8)
                .Select(i => new FeedItem($"{i}", $"Post {i}", $"https://example.com/{i}", null, null, null, null, [new FeedCategory("type", "movie")], null))
                .ToList(),
            CacheValidators.None));

        using var response = await _api.PostAsync("/api/feeds/validate", """{"url":"https://example.com/feed.xml"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("My blog", body.GetProperty("title").GetString());
        Assert.Equal(8, body.GetProperty("itemCount").GetInt32());
        Assert.Equal(5, body.GetProperty("items").GetArrayLength());
        Assert.Equal("movie", body.GetProperty("items")[0].GetProperty("categories")[0].GetProperty("value").GetString());
        var request = Assert.Single(_api.Source.Requests);
        Assert.Null(request.Validators);

        using var list = await _api.GetAsync("/api/feeds");
        Assert.Equal(0, (await ReadAsync(list)).GetArrayLength());
    }

    [Fact]
    public async Task ValidateReportsFetchFailures()
    {
        _api.Source.Returns(new FeedFetchFailed(new FetchError(FetchErrorKind.Parse, "not an RSS or Atom document")));

        using var response = await _api.PostAsync("/api/feeds/validate", """{"url":"https://example.com/page.html"}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("fetch_failed", body.GetProperty("error").GetString());
        Assert.Contains("not an RSS or Atom document", body.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRejectsBadUrlsWithoutFetching()
    {
        using var response = await _api.PostAsync("/api/feeds/validate", """{"url":"not a url"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_api.Source.Requests);
    }
}
