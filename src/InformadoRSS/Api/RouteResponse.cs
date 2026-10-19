using System.Text.Json;

namespace InformadoRSS.Api;

/// <summary>A route with its filter and template as JSON values.</summary>
public sealed record RouteResponse(
    long Id,
    long FeedId,
    string Name,
    bool Enabled,
    JsonElement? Filter,
    JsonElement? Template,
    IReadOnlyList<DestinationResponse> Destinations);
