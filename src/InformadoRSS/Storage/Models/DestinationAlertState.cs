using InformadoRSS.Core;

namespace InformadoRSS.Storage;

/// <summary>A route destination that is flagged as broken, was alerted as broken, or both.</summary>
/// <param name="DestinationId">Row id of the destination; it changes whenever the route destinations are replaced.</param>
/// <param name="Position">Index of the destination in the route's destination list.</param>
/// <param name="BrokenSince">When the destination started failing, or <c>null</c> if it works again.</param>
/// <param name="BrokenReason">The latest error reported by the sink while broken.</param>
/// <param name="AlertedAt">When the "destination broken" alert was sent, or <c>null</c> if it was not sent yet.</param>
public sealed record DestinationAlertState(
    long DestinationId,
    long RouteId,
    string RouteName,
    int Position,
    Destination Destination,
    DateTimeOffset? BrokenSince,
    string? BrokenReason,
    DateTimeOffset? AlertedAt);
