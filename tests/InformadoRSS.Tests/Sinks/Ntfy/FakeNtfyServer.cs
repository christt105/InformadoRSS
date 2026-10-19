using System.Net;
using System.Text;

namespace InformadoRSS.Tests.Sinks.Ntfy;

/// <summary>Records every request and answers with <see cref="Respond"/> (200 with a JSON message by default).</summary>
public sealed class FakeNtfyServer : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"abc","event":"message"}""", Encoding.UTF8, "application/json"),
        };

    public static HttpResponseMessage Error(HttpStatusCode status, string body = "") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests)
        {
            Requests.Add((request, body));
        }

        return Respond(request);
    }
}
