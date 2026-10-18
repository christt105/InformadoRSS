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
