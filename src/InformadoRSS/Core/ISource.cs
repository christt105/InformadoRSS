namespace InformadoRSS.Core;

/// <summary>Where a source fetches its items from.</summary>
public sealed record FeedDefinition(Uri Url);

/// <summary>Fetches normalized items from an origin such as an RSS or Atom feed.</summary>
public interface ISource
{
    Task<IReadOnlyList<FeedItem>> FetchAsync(FeedDefinition feed, CancellationToken cancellationToken);
}
