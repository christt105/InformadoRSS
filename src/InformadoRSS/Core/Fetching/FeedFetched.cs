namespace InformadoRSS.Core;

/// <summary>The feed was downloaded and parsed.</summary>
/// <param name="Title">Title of the feed itself, if it declares one.</param>
/// <param name="Items">Items in document order.</param>
/// <param name="Validators">Validators to send on the next fetch.</param>
public sealed record FeedFetched(string? Title, IReadOnlyList<FeedItem> Items, CacheValidators Validators) : FetchResult;
