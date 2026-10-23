namespace InformadoRSS.Api;

/// <param name="MaxItemAgeSeconds">Items published longer ago than this are skipped by the scheduler.</param>
/// <param name="Items">The feed items, newest first.</param>
public sealed record FeedItemsResponse(long MaxItemAgeSeconds, IReadOnlyList<FeedItemStatus> Items);
