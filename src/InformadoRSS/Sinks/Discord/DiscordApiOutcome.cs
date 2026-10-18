namespace InformadoRSS.Sinks.Discord;

public enum DiscordApiOutcome
{
    Success,

    /// <summary>No bot token is configured; nothing was sent.</summary>
    MissingToken,

    /// <summary>Still rate limited after the allowed retries or wait; see <see cref="DiscordApiResponse.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>Discord answered with an error status other than 429.</summary>
    HttpError,

    /// <summary>The request did not get an answer (connection error or timeout).</summary>
    NetworkError,
}
