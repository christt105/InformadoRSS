namespace InformadoRSS.Api;

/// <param name="ItemId">Id of the item, as listed by the items endpoint.</param>
/// <param name="RouteId">Route to reset the item for; every route of the feed when omitted.</param>
public sealed record ItemResetRequest(string? ItemId, long? RouteId);
