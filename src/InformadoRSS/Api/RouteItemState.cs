namespace InformadoRSS.Api;

/// <param name="Seen">Whether the route already handled the item (sent, filtered out, or skipped for age).</param>
public sealed record RouteItemState(long RouteId, string RouteName, bool Seen);
