using System.Net;
using System.Net.Http.Headers;
using InformadoRSS.Core;

namespace InformadoRSS.Sources;

/// <summary>Downloads RSS and Atom feeds over HTTP with conditional requests and parses them with <see cref="FeedParser"/>.</summary>
public sealed class RssAtomSource(HttpClient httpClient, RssAtomSourceOptions options) : ISource
{
    /// <summary>User-Agent sent with every request.</summary>
    public static string UserAgent { get; } = $"InformadoRSS/{AppInfo.Version} (+https://github.com/christt105/InformadoRSS)";

    private const string AcceptHeader =
        "application/rss+xml, application/atom+xml, application/rdf+xml;q=0.9, application/xml;q=0.9, text/xml;q=0.8, */*;q=0.5";

    public async Task<FetchResult> FetchAsync(FeedDefinition feed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var previous = feed.Validators ?? CacheValidators.None;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, feed.Url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", AcceptHeader);
        if (previous.ETag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", previous.ETag);
        }

        if (previous.LastModified is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Modified-Since", previous.LastModified);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var validators = ReadValidators(response);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new FeedNotModified(validators.IsEmpty ? previous : validators);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Fail(
                    FetchErrorKind.HttpStatus,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd(),
                    response.StatusCode,
                    ReadRetryAfter(response.Headers.RetryAfter));
            }

            if (response.Content.Headers.ContentLength > options.MaxResponseBytes)
            {
                return TooLarge();
            }

            using var body = new MemoryStream();
            await using (var stream = await response.Content.ReadAsStreamAsync(timeout.Token))
            {
                if (!await CopyWithLimitAsync(stream, body, options.MaxResponseBytes, timeout.Token))
                {
                    return TooLarge();
                }
            }

            body.Position = 0;
            var parsed = FeedParser.Parse(body, response.RequestMessage?.RequestUri ?? feed.Url);
            return new FeedFetched(parsed.Title, parsed.Items, validators);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(FetchErrorKind.Timeout, $"No complete response within {options.Timeout.TotalSeconds:0.###} s.");
        }
        catch (HttpRequestException ex)
        {
            return Fail(FetchErrorKind.Network, ex.Message);
        }
        catch (IOException ex)
        {
            return Fail(FetchErrorKind.Network, ex.Message);
        }
        catch (FeedParseException ex)
        {
            return Fail(FetchErrorKind.Parse, ex.Message);
        }
    }

    private FeedFetchFailed TooLarge() =>
        Fail(FetchErrorKind.TooLarge, $"The response is larger than {options.MaxResponseBytes} bytes.");

    private static FeedFetchFailed Fail(FetchErrorKind kind, string message, HttpStatusCode? statusCode = null, TimeSpan? retryAfter = null) =>
        new(new FetchError(kind, message, statusCode, retryAfter));

    private static CacheValidators ReadValidators(HttpResponseMessage response)
    {
        var etag = response.Headers.TryGetValues("ETag", out var etags) ? etags.FirstOrDefault() : null;
        var lastModified = response.Content.Headers.TryGetValues("Last-Modified", out var dates) ? dates.FirstOrDefault() : null;
        return new CacheValidators(NullIfBlank(etag), NullIfBlank(lastModified));
    }

    private static TimeSpan? ReadRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    private static async Task<bool> CopyWithLimitAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
            {
                return false;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
