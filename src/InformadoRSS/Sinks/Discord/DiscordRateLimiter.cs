using System.Globalization;
using System.Net.Http.Headers;

namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Remembers until when each rate-limit bucket (keyed by route and major parameter, e.g. the channel id) and the
/// global limit are exhausted. Shared by every <see cref="DiscordRestClient"/> instance.
/// </summary>
public sealed class DiscordRateLimiter(TimeProvider timeProvider)
{
    private const int PruneThreshold = 256;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _blockedUntil = new(StringComparer.Ordinal);
    private DateTimeOffset _globalBlockedUntil = DateTimeOffset.MinValue;

    /// <summary>How long a request to <paramref name="bucket"/> has to wait; <see cref="TimeSpan.Zero"/> if none.</summary>
    public TimeSpan GetDelay(string bucket)
    {
        var now = timeProvider.GetUtcNow();
        lock (_lock)
        {
            var until = _globalBlockedUntil;
            if (_blockedUntil.TryGetValue(bucket, out var bucketUntil))
            {
                if (bucketUntil <= now)
                {
                    _blockedUntil.Remove(bucket);
                }
                else if (bucketUntil > until)
                {
                    until = bucketUntil;
                }
            }

            return until > now ? until - now : TimeSpan.Zero;
        }
    }

    /// <summary>Reads <c>X-RateLimit-Remaining</c> and <c>X-RateLimit-Reset-After</c> from a response.</summary>
    public void Update(string bucket, HttpResponseHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!TryReadNumber(headers, "X-RateLimit-Remaining", out var remaining))
        {
            return;
        }

        if (remaining > 0)
        {
            lock (_lock)
            {
                _blockedUntil.Remove(bucket);
            }

            return;
        }

        if (TryReadNumber(headers, "X-RateLimit-Reset-After", out var resetAfter) && resetAfter > 0)
        {
            Block(bucket, TimeSpan.FromSeconds(resetAfter), global: false);
        }
    }

    /// <summary>Blocks <paramref name="bucket"/>, or every bucket when <paramref name="global"/>, for <paramref name="duration"/>.</summary>
    public void Block(string bucket, TimeSpan duration, bool global)
    {
        var until = timeProvider.GetUtcNow() + duration;
        lock (_lock)
        {
            if (global)
            {
                if (until > _globalBlockedUntil)
                {
                    _globalBlockedUntil = until;
                }

                return;
            }

            if (_blockedUntil.Count >= PruneThreshold)
            {
                Prune();
            }

            if (!_blockedUntil.TryGetValue(bucket, out var current) || until > current)
            {
                _blockedUntil[bucket] = until;
            }
        }
    }

    private void Prune()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var expired in _blockedUntil.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToList())
        {
            _blockedUntil.Remove(expired);
        }
    }

    internal static bool TryReadNumber(HttpHeaders headers, string name, out double value)
    {
        value = 0;
        return headers.TryGetValues(name, out var values)
            && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
