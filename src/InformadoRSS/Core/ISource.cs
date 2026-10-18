namespace InformadoRSS.Core;

/// <summary>Where a source fetches its items from, plus the validators returned by the previous successful fetch.</summary>
public sealed record FeedDefinition(Uri Url, CacheValidators? Validators = null);

/// <summary>Fetches normalized items from an origin such as an RSS or Atom feed.</summary>
public interface ISource
{
    /// <summary>
    /// Fetches the feed. Expected failures (HTTP errors, timeouts, unparsable content) are returned as
    /// <see cref="FeedFetchFailed"/>; only cancellation through <paramref name="cancellationToken"/> throws.
    /// </summary>
    Task<FetchResult> FetchAsync(FeedDefinition feed, CancellationToken cancellationToken);
}
