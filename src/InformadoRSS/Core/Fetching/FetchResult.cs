namespace InformadoRSS.Core;

/// <summary>Outcome of <see cref="ISource.FetchAsync"/>: <see cref="FeedFetched"/>, <see cref="FeedNotModified"/> or <see cref="FeedFetchFailed"/>.</summary>
public abstract record FetchResult;
