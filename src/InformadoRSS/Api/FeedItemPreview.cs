using InformadoRSS.Core;

namespace InformadoRSS.Api;

/// <summary>The fields of a <see cref="FeedItem"/> shown when checking a feed URL.</summary>
public sealed record FeedItemPreview(
    string Id,
    string? Title,
    string? Link,
    string? Author,
    DateTimeOffset? Published,
    IReadOnlyList<FeedCategory> Categories,
    string? ImageUrl);
