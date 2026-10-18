using System.Net;

namespace InformadoRSS.Core;

/// <summary>Why a fetch failed.</summary>
/// <param name="StatusCode">Set when <see cref="Kind"/> is <see cref="FetchErrorKind.HttpStatus"/>.</param>
/// <param name="RetryAfter">Delay requested by the server through <c>Retry-After</c>, if any.</param>
public sealed record FetchError(FetchErrorKind Kind, string Message, HttpStatusCode? StatusCode = null, TimeSpan? RetryAfter = null);
