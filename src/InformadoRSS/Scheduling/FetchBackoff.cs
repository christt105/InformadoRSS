namespace InformadoRSS.Scheduling;

/// <summary>Computes when a feed should be fetched again.</summary>
public static class FetchBackoff
{
    /// <summary>
    /// The delay before the next fetch: the feed interval while it is healthy, doubled for every consecutive failure after
    /// the first and capped at <paramref name="maxBackoff"/> (or the interval, if longer). A server <c>Retry-After</c> is
    /// honoured when it asks for more, up to <paramref name="maxRetryAfter"/>.
    /// </summary>
    public static TimeSpan NextDelay(
        TimeSpan interval, int consecutiveFailures, TimeSpan maxBackoff, TimeSpan? retryAfter, TimeSpan maxRetryAfter)
    {
        if (consecutiveFailures <= 0)
        {
            return interval;
        }

        var cap = interval > maxBackoff ? interval : maxBackoff;
        var shift = Math.Min(consecutiveFailures - 1, 32);
        var delay = interval.Ticks > cap.Ticks >> shift ? cap : TimeSpan.FromTicks(interval.Ticks << shift);

        if (retryAfter is { } requested)
        {
            var honoured = requested < maxRetryAfter ? requested : maxRetryAfter;
            if (honoured > delay)
            {
                delay = honoured;
            }
        }

        return delay;
    }
}
