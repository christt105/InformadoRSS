using System.Net;

namespace InformadoRSS.Core;

/// <summary>
/// HTTP cache validators of a feed, stored verbatim as received in the <c>ETag</c> and <c>Last-Modified</c>
/// response headers and sent back as <c>If-None-Match</c> and <c>If-Modified-Since</c>.
/// </summary>
public sealed record CacheValidators(string? ETag, string? LastModified)
{
    public static CacheValidators None { get; } = new(null, null);

    public bool IsEmpty => ETag is null && LastModified is null;
}

/// <summary>Outcome of <see cref="ISource.FetchAsync"/>: <see cref="FeedFetched"/>, <see cref="FeedNotModified"/> or <see cref="FeedFetchFailed"/>.</summary>
public abstract record FetchResult;

/// <summary>The feed was downloaded and parsed.</summary>
/// <param name="Title">Title of the feed itself, if it declares one.</param>
/// <param name="Items">Items in document order.</param>
/// <param name="Validators">Validators to send on the next fetch.</param>
public sealed record FeedFetched(string? Title, IReadOnlyList<FeedItem> Items, CacheValidators Validators) : FetchResult;

/// <summary>The server answered <c>304 Not Modified</c>; there is nothing new to process.</summary>
/// <param name="Validators">Validators to send on the next fetch (the new ones if the server sent any, otherwise the previous ones).</param>
public sealed record FeedNotModified(CacheValidators Validators) : FetchResult;

/// <summary>The fetch failed; <see cref="Error"/> tells why, for health reporting and backoff.</summary>
public sealed record FeedFetchFailed(FetchError Error) : FetchResult;

public enum FetchErrorKind
{
    /// <summary>The server answered with a non-success status code (see <see cref="FetchError.StatusCode"/>).</summary>
    HttpStatus,

    /// <summary>The request or the download of the body took longer than the configured timeout.</summary>
    Timeout,

    /// <summary>Connection-level failure: DNS, TCP, TLS, connection reset.</summary>
    Network,

    /// <summary>The body was downloaded but is not a well-formed RSS or Atom document.</summary>
    Parse,

    /// <summary>The body exceeded the configured size limit.</summary>
    TooLarge,
}

/// <summary>Why a fetch failed.</summary>
/// <param name="StatusCode">Set when <see cref="Kind"/> is <see cref="FetchErrorKind.HttpStatus"/>.</param>
/// <param name="RetryAfter">Delay requested by the server through <c>Retry-After</c>, if any.</param>
public sealed record FetchError(FetchErrorKind Kind, string Message, HttpStatusCode? StatusCode = null, TimeSpan? RetryAfter = null);
