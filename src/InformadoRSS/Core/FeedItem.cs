namespace InformadoRSS.Core;

/// <summary>A feed entry normalized from any source format. Filters and templates only work on this shape.</summary>
public sealed record FeedItem(
    string Id,
    string? Title,
    string? Link,
    string? DescriptionHtml,
    string? DescriptionText,
    string? Author,
    DateTimeOffset? Published,
    IReadOnlyList<FeedCategory> Categories,
    string? ImageUrl);
