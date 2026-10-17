using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InformadoRSS.Tests;

public sealed class SpaFallbackTests : IDisposable
{
    private const string IndexHtml = "<!doctype html><title>InformadoRSS</title>";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("informadorss-wwwroot-").FullName;
    private readonly WebApplicationFactory<Program> _factory;

    public SpaFallbackTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(_webRoot, "app.js"), "console.log(1)");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/feeds/42")]
    public async Task ClientRoutesServeIndexHtml(string path)
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(IndexHtml, body);
    }

    [Fact]
    public async Task StaticFilesAreServed()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync(new Uri("/app.js", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("console.log(1)", body);
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/missing.js")]
    public async Task UnknownApiRoutesAndFilesReturnNotFound(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }
}
