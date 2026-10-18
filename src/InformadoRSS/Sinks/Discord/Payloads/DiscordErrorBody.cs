namespace InformadoRSS.Sinks.Discord;

/// <summary>A Discord error response, including the extra fields of a 429.</summary>
/// <param name="Code">Discord's JSON error code, e.g. <c>50001</c> (missing access).</param>
/// <param name="RetryAfter">Seconds to wait before retrying, on a 429.</param>
/// <param name="Global">Whether a 429 applies to every route, not just one bucket.</param>
public sealed record DiscordErrorBody(int? Code = null, string? Message = null, double? RetryAfter = null, bool? Global = null);
