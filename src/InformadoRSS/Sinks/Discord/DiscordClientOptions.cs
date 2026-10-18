namespace InformadoRSS.Sinks.Discord;

/// <summary>How long a single <see cref="DiscordRestClient"/> call may wait for rate limits before giving up.</summary>
public sealed class DiscordClientOptions
{
    /// <summary>Retries after a 429 within one call.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Longest single wait within one call; longer rate limits are returned to the caller instead.</summary>
    public TimeSpan MaxWait { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
