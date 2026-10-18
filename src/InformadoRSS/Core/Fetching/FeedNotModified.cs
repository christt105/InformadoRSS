namespace InformadoRSS.Core;

/// <summary>The server answered <c>304 Not Modified</c>; there is nothing new to process.</summary>
/// <param name="Validators">Validators to send on the next fetch (the new ones if the server sent any, otherwise the previous ones).</param>
public sealed record FeedNotModified(CacheValidators Validators) : FetchResult;
