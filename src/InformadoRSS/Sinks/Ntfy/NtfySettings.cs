using InformadoRSS.Storage;

namespace InformadoRSS.Sinks.Ntfy;

/// <summary>
/// The ntfy server used by <see cref="NtfyClient"/>, read from the <see cref="UrlKey"/> and <see cref="TokenKey"/> settings.
/// </summary>
/// <param name="Url">The server base URL, or <c>null</c> when it is missing or not an absolute http(s) URL.</param>
/// <param name="Token">An optional access token, sent as a bearer token.</param>
public sealed record NtfySettings(Uri? Url, string? Token)
{
    public const string UrlKey = "ntfy.url";
    public const string TokenKey = "ntfy.token";

    public static async Task<NtfySettings> LoadAsync(ISettingsStore settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var url = (await settings.GetAsync(UrlKey, cancellationToken))?.Trim();
        var token = (await settings.GetAsync(TokenKey, cancellationToken))?.Trim();
        return new NtfySettings(ParseUrl(url), string.IsNullOrEmpty(token) ? null : token);
    }

    /// <summary>Whether <paramref name="topic"/> is a valid ntfy topic name: 1 to 64 ASCII letters, digits, '-' or '_'.</summary>
    public static bool IsValidTopic(string? topic) =>
        topic is { Length: > 0 and <= 64 } && topic.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static Uri? ParseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
}
