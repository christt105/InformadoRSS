using InformadoRSS.Templates;

namespace InformadoRSS.Sinks.Discord;

/// <summary>Error body of the <c>/api/discord</c> endpoints.</summary>
/// <param name="Error">
/// A stable code: <c>invalid_id</c>, <c>invalid_body</c>, <c>invalid_template</c>, <c>token_missing</c>, <c>token_invalid</c>, <c>forbidden</c>,
/// <c>not_found</c>, <c>rate_limited</c>, <c>discord_unavailable</c> or <c>discord_error</c>.
/// </param>
/// <param name="Message">A human-readable explanation, including Discord's own error when there is one.</param>
/// <param name="RetryAfterSeconds">For <c>rate_limited</c>, how long to wait before trying again.</param>
/// <param name="TemplateErrors">For <c>invalid_template</c>, every problem found in the template.</param>
public sealed record DiscordEndpointError(
    string Error,
    string Message,
    double? RetryAfterSeconds = null,
    IReadOnlyList<TemplateError>? TemplateErrors = null);
