namespace InformadoRSS.Import;

/// <summary>What happened when force-sending the latest item of one route.</summary>
/// <param name="ItemTitle">The item that was sent or attempted, if any.</param>
/// <param name="Errors">One entry per destination that failed, or the fetch or filter error.</param>
public sealed record SendLatestResult(
    long FeedId,
    string FeedName,
    long RouteId,
    string RouteName,
    SendLatestStatus Status,
    string? ItemTitle,
    string? ItemLink,
    IReadOnlyList<string> Errors);
