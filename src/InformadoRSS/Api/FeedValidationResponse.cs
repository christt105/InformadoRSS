namespace InformadoRSS.Api;

/// <summary>A feed that was fetched and parsed without being saved.</summary>
/// <param name="Url">The normalized URL that was fetched.</param>
/// <param name="Title">Title the feed declares, if any.</param>
/// <param name="ItemCount">How many items the feed has right now.</param>
/// <param name="Items">The first items in document order, at most <see cref="FeedEndpoints.PreviewItemCount"/>.</param>
public sealed record FeedValidationResponse(string Url, string? Title, int ItemCount, IReadOnlyList<FeedItemPreview> Items);
