using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using InformadoRSS.Core;
using InformadoRSS.Storage;

namespace InformadoRSS.Sinks.Ntfy;

/// <summary>
/// Publishes notifications with a plain <c>POST {url}/{topic}</c> to the server configured in <see cref="NtfySettings"/>.
/// Non-ASCII header values are sent RFC 2047 encoded, which ntfy decodes. Never logs or returns the token.
/// </summary>
public sealed class NtfyClient(HttpClient httpClient, ISettingsStore settings)
{
    /// <summary>How long a single publish may take.</summary>
    public static TimeSpan RequestTimeout { get; } = TimeSpan.FromSeconds(30);

    private const int MaxErrorLength = 200;

    public async Task<DeliveryResult> PublishAsync(string topic, NtfyMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!NtfySettings.IsValidTopic(topic))
        {
            return DeliveryResult.Permanent($"\"{topic}\" is not a valid ntfy topic");
        }

        var server = await NtfySettings.LoadAsync(settings, cancellationToken);
        if (server.Url is null)
        {
            return DeliveryResult.Transient($"ntfy server URL not configured, set \"{NtfySettings.UrlKey}\" in the settings");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{server.Url.AbsoluteUri.TrimEnd('/')}/{topic}"))
        {
            Content = new StringContent(message.Body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.TryAddWithoutValidation("User-Agent", $"InformadoRSS/{AppInfo.Version}");
        AddHeader(request, "Title", message.Title);
        AddHeader(request, "Priority", message.Priority?.ToString(CultureInfo.InvariantCulture));
        AddHeader(request, "Tags", message.Tags is { Count: > 0 } tags ? string.Join(',', tags) : null);
        AddHeader(request, "Click", message.Click);
        if (server.Token is { } token)
        {
            request.Headers.Authorization = token.Contains(':', StringComparison.Ordinal)
                ? new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token)))
                : new AuthenticationHeaderValue("Bearer", token);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return ToDeliveryResult(response, body, topic);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeliveryResult.Transient("request to ntfy timed out");
        }
        catch (HttpRequestException ex)
        {
            return DeliveryResult.Transient($"could not reach ntfy: {ex.Message}");
        }
    }

    private static DeliveryResult ToDeliveryResult(HttpResponseMessage response, string body, string topic)
    {
        if (response.IsSuccessStatusCode)
        {
            return DeliveryResult.Delivered;
        }

        var status = (int)response.StatusCode;
        var detail = Describe(status, body);
        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => DeliveryResult.Transient($"rate limited by ntfy ({detail})", response.Headers.RetryAfter?.Delta),
            HttpStatusCode.Unauthorized => DeliveryResult.Transient($"ntfy rejected the access token, check it in the settings ({detail})"),
            HttpStatusCode.NotFound => DeliveryResult.Transient($"ntfy server not found, check its URL in the settings ({detail})"),
            HttpStatusCode.Forbidden => DeliveryResult.Permanent($"ntfy topic {topic} is not writable with the configured access ({detail})"),
            HttpStatusCode.RequestTimeout => DeliveryResult.Transient($"ntfy is unavailable ({detail})"),
            _ when status >= 500 => DeliveryResult.Transient($"ntfy is unavailable ({detail})"),
            _ => DeliveryResult.Rejected($"ntfy rejected the message ({detail})"),
        };
    }

    private static string Describe(int status, string body)
    {
        var text = body.ReplaceLineEndings(" ").Trim();
        if (text.Length > MaxErrorLength)
        {
            text = text[..MaxErrorLength];
        }

        return text.Length == 0
            ? $"HTTP {status.ToString(CultureInfo.InvariantCulture)}"
            : $"HTTP {status.ToString(CultureInfo.InvariantCulture)}, {text}";
    }

    private static void AddHeader(HttpRequestMessage request, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation(name, EncodeHeader(value));
    }

    /// <summary>Flattens the value to one line and RFC 2047 encodes it unless it is printable ASCII.</summary>
    internal static string EncodeHeader(string value)
    {
        var flat = value.ReplaceLineEndings(" ").Trim();
        return flat.All(c => c is >= ' ' and <= '~')
            ? flat
            : $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(flat))}?=";
    }
}
