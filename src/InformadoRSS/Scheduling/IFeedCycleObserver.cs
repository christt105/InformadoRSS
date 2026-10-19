using InformadoRSS.Storage;

namespace InformadoRSS.Scheduling;

/// <summary>
/// Notified by the <see cref="FeedScheduler"/> after every completed <see cref="FeedProcessor"/> cycle, once the outcome
/// and destination state are stored. Resolved from the cycle's service scope; exceptions are logged and do not affect
/// scheduling.
/// </summary>
public interface IFeedCycleObserver
{
    /// <param name="feed">The feed as it was loaded before the cycle.</param>
    Task OnFeedProcessedAsync(Feed feed, FeedOutcome outcome, CancellationToken cancellationToken);
}
