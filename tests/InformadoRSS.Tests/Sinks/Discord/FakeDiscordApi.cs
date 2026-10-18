using System.Net;
using System.Text;

namespace InformadoRSS.Tests.Sinks.Discord;

/// <summary>
/// Answers Discord API requests from a table keyed by method and path (with query), e.g. <c>GET guilds/1/channels</c>.
/// Unknown requests get a 404 with Discord's error shape.
/// </summary>
public sealed class FakeDiscordApi : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public IEnumerable<string> RequestedRoutes => Requests.Select(request => Key(request.Request));

    public FakeDiscordApi On(string route, Func<HttpResponseMessage> respond)
    {
        _routes[route] = respond;
        return this;
    }

    public FakeDiscordApi On(string route, string json) => On(route, () => Json(HttpStatusCode.OK, json));

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests)
        {
            Requests.Add((request, body));
        }

        return _routes.TryGetValue(Key(request), out var respond)
            ? respond()
            : Json(HttpStatusCode.NotFound, """{"message":"404: Not Found","code":0}""");
    }

    private static string Key(HttpRequestMessage request)
    {
        var path = request.RequestUri!.PathAndQuery;
        const string prefix = "/api/v10/";
        return $"{request.Method} {(path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path)}";
    }
}
