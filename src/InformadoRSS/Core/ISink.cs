namespace InformadoRSS.Core;

/// <summary>A message after its template has been applied to a <see cref="FeedItem"/>.</summary>
public sealed record RenderedMessage(string Content);

/// <summary>A target for a sink, e.g. a Discord channel or an ntfy topic.</summary>
public sealed record Destination(string Sink, IReadOnlyDictionary<string, string> Parameters);

/// <summary>Delivers rendered messages to a destination such as Discord or ntfy.</summary>
public interface ISink
{
    /// <summary>Identifier matched against <see cref="Destination.Sink"/>.</summary>
    string Name { get; }

    Task DeliverAsync(Destination destination, RenderedMessage message, CancellationToken cancellationToken);
}
