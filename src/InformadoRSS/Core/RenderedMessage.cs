namespace InformadoRSS.Core;

/// <summary>A message after its template has been applied to a <see cref="FeedItem"/>.</summary>
public sealed record RenderedMessage(string Content);
