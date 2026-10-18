namespace InformadoRSS.Scheduling;

/// <summary>Result of one <see cref="FeedProcessor.ProcessAsync"/> cycle, used to schedule the next one.</summary>
/// <param name="Succeeded">Whether the fetch succeeded (a <c>304 Not Modified</c> counts as success).</param>
/// <param name="ConsecutiveFailures">Failures in a row including this cycle; zero after a success.</param>
/// <param name="RetryAfter">Delay requested by the server, if any.</param>
public sealed record FeedOutcome(bool Succeeded, int ConsecutiveFailures, TimeSpan? RetryAfter = null)
{
    public static FeedOutcome Success { get; } = new(true, 0);
}
