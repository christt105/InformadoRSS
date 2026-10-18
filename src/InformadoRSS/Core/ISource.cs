namespace InformadoRSS.Core;

/// <summary>Fetches normalized items from an origin such as an RSS or Atom feed.</summary>
public interface ISource
{
    /// <summary>
    /// Fetches the feed. Expected failures (HTTP errors, timeouts, unparsable content) are returned as
    /// <see cref="FeedFetchFailed"/>; only cancellation through <paramref name="cancellationToken"/> throws.
    /// </summary>
    Task<FetchResult> FetchAsync(FeedDefinition feed, CancellationToken cancellationToken);
}
