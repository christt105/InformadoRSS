namespace InformadoRSS.Storage;

/// <summary>
/// Remembers which broken feeds and destinations were already alerted, so alerts are sent once per transition and survive
/// restarts. State is dropped together with its feed or destination.
/// </summary>
public interface IAlertStateStore
{
    Task<FeedAlertState?> GetFeedStateAsync(long feedId, CancellationToken cancellationToken);

    /// <summary>Stores the state of the feed; does nothing if the feed no longer exists.</summary>
    Task SetFeedStateAsync(long feedId, FeedAlertState state, CancellationToken cancellationToken);

    Task ClearFeedStateAsync(long feedId, CancellationToken cancellationToken);

    /// <summary>
    /// Destinations of the feed's routes that are flagged as broken or were alerted as broken, ordered by route and position.
    /// </summary>
    Task<IReadOnlyList<DestinationAlertState>> ListDestinationStatesAsync(long feedId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that the destination was alerted as broken at <paramref name="at"/>, or forgets it when <paramref name="at"/>
    /// is <c>null</c>. Does nothing if the destination no longer exists.
    /// </summary>
    Task SetDestinationAlertedAsync(long destinationId, DateTimeOffset? at, CancellationToken cancellationToken);
}
