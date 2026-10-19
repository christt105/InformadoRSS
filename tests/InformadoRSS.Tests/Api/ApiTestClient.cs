using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InformadoRSS.Core;
using InformadoRSS.Tests.Scheduling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Tests.Api;

/// <summary>The app on a throwaway database with a <see cref="FakeSource"/>, plus JSON helpers for the API tests.</summary>
public sealed class ApiTestClient : IAsyncDisposable
{
    private readonly AppFactory _factory = new();

    public ApiTestClient()
    {
        var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.RemoveAll<ISource>().AddSingleton<ISource>(Source)));
        Http = app.CreateClient();
        Services = app.Services;
    }

    public FakeSource Source { get; } = new();

    public HttpClient Http { get; }

    public IServiceProvider Services { get; }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public Task<HttpResponseMessage> GetAsync(string path) => Http.GetAsync(Url(path), Cancel);

    public Task<HttpResponseMessage> DeleteAsync(string path) => Http.DeleteAsync(Url(path), Cancel);

    public Task<HttpResponseMessage> PostAsync(string path, string json) => Http.PostAsync(Url(path), Json(json), Cancel);

    public Task<HttpResponseMessage> PutAsync(string path, string json) => Http.PutAsync(Url(path), Json(json), Cancel);

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        return document.RootElement.Clone();
    }

    public async Task<long> CreateFeedAsync(string url = "https://example.com/feed.xml", string name = "Feed")
    {
        using var response = await Http.PostAsJsonAsync(Url("/api/feeds"), new { url, name }, Cancel);
        response.EnsureSuccessStatusCode();
        return (await ReadAsync(response)).GetProperty("id").GetInt64();
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _factory.DisposeAsync();
    }

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
