using System.Collections.Concurrent;
using InformadoRSS.Scheduling;
using InformadoRSS.Storage;

namespace InformadoRSS.Tests.Scheduling;

/// <summary>Records every notification, then throws if <see cref="Throws"/> is set.</summary>
public sealed class RecordingObserver(bool throws = false) : IFeedCycleObserver
{
    public bool Throws { get; } = throws;

    public ConcurrentQueue<(long FeedId, FeedOutcome Outcome)> Calls { get; } = new();

    public Task OnFeedProcessedAsync(Feed feed, FeedOutcome outcome, CancellationToken cancellationToken)
    {
        Calls.Enqueue((feed.Id, outcome));
        return Throws ? throw new InvalidOperationException("observer failed") : Task.CompletedTask;
    }
}
