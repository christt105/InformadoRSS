namespace InformadoRSS.Sinks.Discord;

/// <summary>The result of a <see cref="DiscordRestClient"/> call. Expected failures are returned, not thrown.</summary>
/// <param name="StatusCode">The HTTP status, when Discord answered.</param>
/// <param name="ErrorCode">Discord's JSON error code, e.g. <c>10003</c> (unknown channel).</param>
/// <param name="Message">Discord's error message, or a description of the network error.</param>
/// <param name="Body">The response body on success.</param>
public sealed record DiscordApiResponse(
    DiscordApiOutcome Outcome,
    int? StatusCode = null,
    int? ErrorCode = null,
    string? Message = null,
    TimeSpan? RetryAfter = null,
    string? Body = null)
{
    public bool IsSuccess => Outcome == DiscordApiOutcome.Success;
}
