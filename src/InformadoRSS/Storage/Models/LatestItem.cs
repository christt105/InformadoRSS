namespace InformadoRSS.Storage;

/// <summary>The newest item of a feed as of its last fetch that returned items, for the feed list.</summary>
/// <param name="FetchedAt">When the fetch that returned the item happened.</param>
public sealed record LatestItem(string Id, string? Title, string? Link, DateTimeOffset? Published, DateTimeOffset FetchedAt);
