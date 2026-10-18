namespace InformadoRSS.Core;

/// <summary>The fetch failed; <see cref="Error"/> tells why, for health reporting and backoff.</summary>
public sealed record FeedFetchFailed(FetchError Error) : FetchResult;
