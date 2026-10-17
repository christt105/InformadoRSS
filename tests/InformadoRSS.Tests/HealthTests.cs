using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InformadoRSS.Tests;

public sealed class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthReturnsOkStatusAndVersion()
    {
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<HealthResponse>("/health", TestContext.Current.CancellationToken);

        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.False(string.IsNullOrWhiteSpace(health.Version));
    }

    [Fact]
    public async Task UnknownApiRouteReturnsNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheckCommandSucceedsAgainstHealthyServer()
    {
        using var client = factory.CreateClient();

        var exitCode = await HealthCheckCommand.RunAsync(client, new Uri(client.BaseAddress!, "/health"), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task HealthCheckCommandFailsOnErrorStatus()
    {
        using var client = factory.CreateClient();

        var exitCode = await HealthCheckCommand.RunAsync(client, new Uri(client.BaseAddress!, "/api/does-not-exist"), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }
}
