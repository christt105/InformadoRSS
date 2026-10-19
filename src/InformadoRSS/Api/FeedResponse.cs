using InformadoRSS.Storage;

namespace InformadoRSS.Api;

/// <summary>A feed with its health, newest item and routes.</summary>
/// <param name="IdentityPattern">See <see cref="FeedSettings.IdentityPattern"/>.</param>
/// <param name="NextFetchAt">When the scheduler fetches the feed next, if it is known and not fetching it right now.</param>
public sealed record FeedResponse(
    long Id,
    string Url,
    string Name,
    long IntervalSeconds,
    string? IdentityPattern,
    FeedHealth Health,
    LatestItem? LatestItem,
    DateTimeOffset? NextFetchAt,
    IReadOnlyList<RouteResponse> Routes);
