namespace InformadoRSS.Storage;

public sealed record FeedSnapshot(
    Uri Url, string Name, long IntervalSeconds, IReadOnlyList<RouteSnapshot> Routes, string? IdentityPattern = null);
