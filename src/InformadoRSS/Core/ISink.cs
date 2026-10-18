namespace InformadoRSS.Core;

/// <summary>Delivers rendered messages to a destination such as Discord or ntfy.</summary>
public interface ISink
{
    /// <summary>Identifier matched against <see cref="Destination.Sink"/>.</summary>
    string Name { get; }

    Task<DeliveryResult> DeliverAsync(Destination destination, RenderedMessage message, CancellationToken cancellationToken);
}
