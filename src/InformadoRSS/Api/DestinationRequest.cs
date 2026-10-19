namespace InformadoRSS.Api;

/// <summary>A destination in a <see cref="RouteRequest"/>.</summary>
/// <param name="Sink">The sink name, e.g. <c>discord</c>.</param>
/// <param name="Parameters">Sink parameters, e.g. <c>guildId</c> and <c>channelId</c> for Discord.</param>
public sealed record DestinationRequest(string? Sink, IReadOnlyDictionary<string, string?>? Parameters = null);
