using System.Net;
using System.Text.Json;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection;
using static InformadoRSS.Tests.Api.ApiTestClient;

namespace InformadoRSS.Tests.Api;

public sealed class RouteEndpointsTests : IAsyncDisposable
{
    private const string ValidRoute = """
        {
          "name": "Movies",
          "filter": {"type":"all","children":[{"type":"condition","field":"category","domain":"type","operator":"equals","value":"movie"}]},
          "template": {"content":"**{{title}}**\n{{link}}"},
          "destinations": [{"sink":"discord","parameters":{"guildId":"100","channelId":"301"}}]
        }
        """;

    private readonly ApiTestClient _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task RouteIsCreatedReadAndListedWithTheFeed()
    {
        var feedId = await _api.CreateFeedAsync();

        using var created = await _api.PostAsync($"/api/feeds/{feedId}/routes", ValidRoute);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = await ReadAsync(created);
        var id = route.GetProperty("id").GetInt64();
        Assert.True(route.GetProperty("enabled").GetBoolean());
        Assert.Equal("category", route.GetProperty("filter").GetProperty("children")[0].GetProperty("field").GetString());
        Assert.Equal("**{{title}}**\n{{link}}", route.GetProperty("template").GetProperty("content").GetString());
        var destination = route.GetProperty("destinations")[0];
        Assert.Equal("301", destination.GetProperty("parameters").GetProperty("channelId").GetString());
        Assert.Equal(JsonValueKind.Null, destination.GetProperty("brokenSince").ValueKind);

        using var read = await _api.GetAsync($"/api/routes/{id}");
        Assert.Equal("Movies", (await ReadAsync(read)).GetProperty("name").GetString());

        using var feed = await _api.GetAsync($"/api/feeds/{feedId}");
        Assert.Equal(id, (await ReadAsync(feed)).GetProperty("routes")[0].GetProperty("id").GetInt64());

        using var list = await _api.GetAsync($"/api/feeds/{feedId}/routes");
        Assert.Equal(1, (await ReadAsync(list)).GetArrayLength());
    }

    [Fact]
    public async Task NullFilterAndTemplateAreStoredAsAbsent()
    {
        var feedId = await _api.CreateFeedAsync();

        using var created = await _api.PostAsync(
            $"/api/feeds/{feedId}/routes",
            """{"name":"All","filter":null,"destinations":[{"sink":"discord","parameters":{"channelId":"301"}}]}""");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = await ReadAsync(created);
        Assert.Equal(JsonValueKind.Null, route.GetProperty("filter").ValueKind);
        Assert.Equal(JsonValueKind.Null, route.GetProperty("template").ValueKind);
    }

    [Fact]
    public async Task InvalidRouteReportsEveryProblem()
    {
        var feedId = await _api.CreateFeedAsync();

        using var response = await _api.PostAsync(
            $"/api/feeds/{feedId}/routes",
            """
            {
              "name": " ",
              "filter": {"type":"condition","field":"title","operator":"regex","value":"("},
              "template": {"content":"{{nope}}"},
              "destinations": [{"sink":"carrier-pigeon"},{"sink":"discord","parameters":{"channelId":"general"}}]
            }
            """);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadAsync(response);
        Assert.Equal("invalid_route", error.GetProperty("error").GetString());
        Assert.Equal(
            ["$.name", "$.destinations[0].sink", "$.destinations[1].parameters.channelId"],
            error.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("path").GetString()));
        Assert.Equal("$.value", error.GetProperty("filterErrors")[0].GetProperty("path").GetString());
        Assert.Equal("$.content", error.GetProperty("templateErrors")[0].GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("alerts-123", HttpStatusCode.Created)]
    [InlineData("bad topic", HttpStatusCode.BadRequest)]
    public async Task NtfyDestinationsNeedAValidTopic(string topic, HttpStatusCode expected)
    {
        var feedId = await _api.CreateFeedAsync();

        using var response = await _api.PostAsync(
            $"/api/feeds/{feedId}/routes", $$$"""{"name":"Push","destinations":[{"sink":"ntfy","parameters":{"topic":"{{{topic}}}"}}]}""");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task EnabledRouteNeedsADestination()
    {
        var feedId = await _api.CreateFeedAsync();

        using var enabled = await _api.PostAsync($"/api/feeds/{feedId}/routes", """{"name":"Empty"}""");
        using var disabled = await _api.PostAsync($"/api/feeds/{feedId}/routes", """{"name":"Draft","enabled":false}""");

        Assert.Equal(HttpStatusCode.BadRequest, enabled.StatusCode);
        Assert.Equal(HttpStatusCode.Created, disabled.StatusCode);
    }

    [Fact]
    public async Task ValidateChecksWithoutSaving()
    {
        using var valid = await _api.PostAsync("/api/routes/validate", ValidRoute);
        using var invalid = await _api.PostAsync("/api/routes/validate", """{"name":"x","template":{"embed":{"color":-1,"title":"t"}}}""");

        Assert.True((await ReadAsync(valid)).GetProperty("valid").GetBoolean());
        var body = await ReadAsync(invalid);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.False(body.GetProperty("valid").GetBoolean());
        Assert.Equal("$.embed.color", body.GetProperty("templateErrors")[0].GetProperty("path").GetString());
        Assert.Equal("$.destinations", body.GetProperty("errors")[0].GetProperty("path").GetString());
    }

    [Fact]
    public async Task RouteCanBeReplacedAndDeleted()
    {
        var feedId = await _api.CreateFeedAsync();
        using var created = await _api.PostAsync($"/api/feeds/{feedId}/routes", ValidRoute);
        var id = (await ReadAsync(created)).GetProperty("id").GetInt64();

        using var updated = await _api.PutAsync(
            $"/api/routes/{id}",
            """{"name":"Renamed","enabled":false,"destinations":[{"sink":"discord","parameters":{"channelId":"302"}}]}""");

        var route = await ReadAsync(updated);
        Assert.Equal("Renamed", route.GetProperty("name").GetString());
        Assert.False(route.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, route.GetProperty("filter").ValueKind);

        using var deleted = await _api.DeleteAsync($"/api/routes/{id}");
        using var missing = await _api.GetAsync($"/api/routes/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task UnknownDestinationParametersAreKept()
    {
        var feedId = await _api.CreateFeedAsync();

        using var created = await _api.PostAsync(
            $"/api/feeds/{feedId}/routes",
            """{"name":"Imported","destinations":[{"sink":"discord","parameters":{"channelId":"301","monitorssConnectionId":"abc123"}}]}""");

        var parameters = (await ReadAsync(created)).GetProperty("destinations")[0].GetProperty("parameters");
        Assert.Equal("abc123", parameters.GetProperty("monitorssConnectionId").GetString());
    }

    [Fact]
    public async Task RouteOnMissingFeedIsNotFound()
    {
        using var response = await _api.PostAsync("/api/feeds/999/routes", ValidRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BrokenDestinationsAreReported()
    {
        var ct = TestContext.Current.CancellationToken;
        var feedId = await _api.CreateFeedAsync();
        using var created = await _api.PostAsync($"/api/feeds/{feedId}/routes", ValidRoute);
        var id = (await ReadAsync(created)).GetProperty("id").GetInt64();
        await _api.Services.GetRequiredService<IRouteStore>()
            .SetDestinationBrokenAsync(id, 0, DateTimeOffset.UtcNow, "Missing Access", ct);

        using var feed = await _api.GetAsync($"/api/feeds/{feedId}");

        var destination = (await ReadAsync(feed)).GetProperty("routes")[0].GetProperty("destinations")[0];
        Assert.Equal("Missing Access", destination.GetProperty("brokenReason").GetString());
        Assert.NotEqual(JsonValueKind.Null, destination.GetProperty("brokenSince").ValueKind);
    }

    [Fact]
    public async Task MetaListsSinksAndPlaceholders()
    {
        using var response = await _api.GetAsync("/api/meta");

        var meta = await ReadAsync(response);
        Assert.Contains("discord", meta.GetProperty("sinks").EnumerateArray().Select(s => s.GetString()));
        Assert.Contains("{{title}}", meta.GetProperty("placeholders").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(60, meta.GetProperty("minIntervalSeconds").GetInt64());
    }
}
