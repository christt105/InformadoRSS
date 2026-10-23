namespace InformadoRSS.Api;

/// <summary>An item the feed has right now and what each route did with it.</summary>
/// <param name="TooOld">Whether the publication date is older than the configured maximum item age.</param>
public sealed record FeedItemStatus(
    string Id,
    string? Title,
    string? Link,
    DateTimeOffset? Published,
    bool TooOld,
    IReadOnlyList<RouteItemState> Routes);
