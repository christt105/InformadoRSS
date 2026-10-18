using InformadoRSS.Core;

namespace InformadoRSS.Storage;

/// <summary>A route destination flagged as broken because a sink rejected it permanently.</summary>
/// <param name="Position">Index of the destination in the route's destination list.</param>
/// <param name="Since">When the destination started failing, i.e. the first failure since it last worked.</param>
/// <param name="Reason">The latest error reported by the sink.</param>
public sealed record BrokenDestination(
    long RouteId, long FeedId, int Position, Destination Destination, DateTimeOffset Since, string Reason);
