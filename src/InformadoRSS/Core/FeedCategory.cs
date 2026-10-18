namespace InformadoRSS.Core;

/// <summary>A category of a <see cref="FeedItem"/>, optionally qualified by a domain (e.g. <c>type</c> or <c>status</c>).</summary>
public sealed record FeedCategory(string? Domain, string Value);
