namespace InformadoRSS.Api;

/// <summary>A route destination and, when a sink rejected it permanently, since when and why.</summary>
public sealed record DestinationResponse(
    string Sink,
    IReadOnlyDictionary<string, string> Parameters,
    DateTimeOffset? BrokenSince,
    string? BrokenReason);
