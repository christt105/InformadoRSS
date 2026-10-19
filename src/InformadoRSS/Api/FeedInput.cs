using InformadoRSS.Storage;

namespace InformadoRSS.Api;

/// <summary>Validates feed request bodies and turns them into <see cref="FeedSettings"/>.</summary>
public static class FeedInput
{
    public const long MinIntervalSeconds = 60;
    public const long DefaultIntervalSeconds = 600;
    public const long MaxIntervalSeconds = 7 * 24 * 60 * 60;
    public const int MaxNameLength = 200;

    /// <summary>Parses an absolute <c>http</c> or <c>https</c> URL.</summary>
    /// <returns>The problem with <paramref name="text"/>, or <see langword="null"/> when <paramref name="url"/> is set.</returns>
    public static string? ParseUrl(string? text, out Uri? url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "the URL is required";
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(parsed.Host))
        {
            return "must be an absolute http(s) URL such as https://example.com/feed.xml";
        }

        url = parsed;
        return null;
    }

    /// <returns>Every problem found; when empty, <paramref name="settings"/> holds the feed to store.</returns>
    public static IReadOnlyList<ApiFieldError> Validate(FeedRequest request, out FeedSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(request);

        settings = null;
        var errors = new List<ApiFieldError>();
        if (ParseUrl(request.Url, out var url) is { } urlError)
        {
            errors.Add(new ApiFieldError("$.url", urlError));
        }

        var name = request.Name?.Trim();
        if (name is { Length: > MaxNameLength })
        {
            errors.Add(new ApiFieldError("$.name", $"the name must be at most {MaxNameLength} characters"));
        }

        var interval = request.IntervalSeconds ?? DefaultIntervalSeconds;
        if (interval is < MinIntervalSeconds or > MaxIntervalSeconds)
        {
            errors.Add(new ApiFieldError(
                "$.intervalSeconds", $"the interval must be between {MinIntervalSeconds} and {MaxIntervalSeconds} seconds"));
        }

        if (errors.Count == 0 && url is not null)
        {
            settings = new FeedSettings(url, string.IsNullOrEmpty(name) ? url.Host : name, TimeSpan.FromSeconds(interval));
        }

        return errors;
    }
}
