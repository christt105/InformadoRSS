namespace InformadoRSS.Api;

/// <summary>Body of <c>POST /api/feeds</c> and <c>PUT /api/feeds/{id}</c>.</summary>
/// <param name="Name">Display name; the URL host when blank.</param>
/// <param name="IntervalSeconds">How often to fetch the feed; <see cref="FeedInput.DefaultIntervalSeconds"/> when absent.</param>
/// <param name="IdentityPattern">Regular expression over the item description whose match becomes part of the item id;
/// none when blank.</param>
public sealed record FeedRequest(string? Url, string? Name = null, long? IntervalSeconds = null, string? IdentityPattern = null);
