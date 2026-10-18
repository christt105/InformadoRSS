namespace InformadoRSS.Scheduling;

/// <summary>
/// Remembers until when route destinations asked not to be retried, from the <see cref="Core.DeliveryResult.RetryAfter"/>
/// of a transient failure. Shared by every <see cref="FeedProcessor"/>; kept in memory only.
/// </summary>
public sealed class DestinationBackoff(TimeProvider time)
{
    private const int PruneThreshold = 256;

    private readonly Lock _lock = new();
    private readonly Dictionary<(long RouteId, int Position), DateTimeOffset> _blockedUntil = [];

    /// <summary>Blocks the destination for <paramref name="delay"/>, unless it is already blocked for longer.</summary>
    public void Defer(long routeId, int position, TimeSpan delay)
    {
        var until = time.GetUtcNow() + delay;
        lock (_lock)
        {
            if (_blockedUntil.Count >= PruneThreshold)
            {
                Prune();
            }

            if (!_blockedUntil.TryGetValue((routeId, position), out var current) || until > current)
            {
                _blockedUntil[(routeId, position)] = until;
            }
        }
    }

    /// <summary>How long the destination still has to wait, or <c>null</c> if it may be tried now.</summary>
    public TimeSpan? Remaining(long routeId, int position)
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            if (!_blockedUntil.TryGetValue((routeId, position), out var until))
            {
                return null;
            }

            if (until <= now)
            {
                _blockedUntil.Remove((routeId, position));
                return null;
            }

            return until - now;
        }
    }

    private void Prune()
    {
        var now = time.GetUtcNow();
        foreach (var expired in _blockedUntil.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToList())
        {
            _blockedUntil.Remove(expired);
        }
    }
}
