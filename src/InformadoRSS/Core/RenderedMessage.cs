namespace InformadoRSS.Core;

/// <summary>A message after its template has been applied to a <see cref="FeedItem"/>.</summary>
/// <param name="Content">Plain message text; may be empty when <paramref name="Embed"/> carries the message.</param>
/// <param name="Embed">An optional rich card, for sinks that support one.</param>
public sealed record RenderedMessage(string Content, RenderedEmbed? Embed = null);
