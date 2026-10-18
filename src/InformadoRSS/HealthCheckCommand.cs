namespace InformadoRSS;

/// <summary>
/// Probes the running server's <c>/health</c> endpoint so container health checks work in images without a shell or curl.
/// </summary>
public static class HealthCheckCommand
{
    public const string Flag = "--healthcheck";

    public static Uri DefaultUrl { get; } = new($"http://localhost:{AppInfo.DefaultPort}/health");

    /// <returns>0 when the endpoint answers with a success status code, 1 otherwise.</returns>
    public static async Task<int> RunAsync(HttpClient client, Uri url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await Console.Error.WriteLineAsync($"Health check failed: {ex.Message}");
            return 1;
        }
    }
}
