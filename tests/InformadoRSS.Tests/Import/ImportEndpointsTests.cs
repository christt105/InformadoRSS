using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InformadoRSS.Import;

namespace InformadoRSS.Tests.Import;

public sealed class ImportEndpointsTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static ByteArrayContent Export()
    {
        var content = new ByteArrayContent(MonitorssFixture.Bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    [Fact]
    public async Task ImportsThenReportsEverythingAsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var dry = await client.PostAsync("/api/import/monitorss?dryRun=true&testChannel=900000000000000001&testGuild=900000000000000002", Export(), ct);
        Assert.Equal(HttpStatusCode.OK, dry.StatusCode);
        using (var report = JsonDocument.Parse(await dry.Content.ReadAsStringAsync(ct)))
        {
            Assert.True(report.RootElement.GetProperty("dryRun").GetBoolean());
            Assert.Equal("900000000000000002", report.RootElement.GetProperty("testGuildId").GetString());
            Assert.Equal(22, report.RootElement.GetProperty("routesCreated").GetInt32());
            Assert.Equal("created", report.RootElement.GetProperty("feeds")[0].GetProperty("action").GetString());
        }

        using var first = await client.PostAsync("/api/import/monitorss", Export(), ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.PostAsync("/api/import/monitorss", Export(), ct);
        using var again = JsonDocument.Parse(await second.Content.ReadAsStringAsync(ct));
        Assert.Equal(0, again.RootElement.GetProperty("routesCreated").GetInt32());
        Assert.Equal(22, again.RootElement.GetProperty("routesUnchanged").GetInt32());
    }

    [Theory]
    [InlineData("/api/import/monitorss?testChannel=abc")]
    [InlineData("/api/import/monitorss?testGuild=1")]
    [InlineData("/api/import/send-latest?channel=abc")]
    public async Task RejectsInvalidIds(string url)
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(url, Export(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsDocumentsThatAreNotExports()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync("/api/import/monitorss", new StringContent("{\"nope\":1}"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("invalid_document", body.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData]
    [InlineData("import-monitorss")]
    [InlineData("import-monitorss", "a.json", "--test-channel", "x")]
    [InlineData("import-monitorss", "a.json", "--test-guild", "1")]
    [InlineData("send-latest", "--feed", "x")]
    [InlineData("send-latest", "--dry-run")]
    public async Task CommandRejectsInvalidArguments(params string[] args) =>
        Assert.Equal(2, await ImportCommand.RunAsync(args, TestContext.Current.CancellationToken));
}
