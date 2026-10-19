namespace InformadoRSS.Import;

/// <param name="ChannelId">When set, sends to this Discord channel instead of the route destinations.</param>
/// <param name="FeedId">Restricts the run to one feed.</param>
/// <param name="IncludeDisabled">Also sends for disabled routes, e.g. imported routes that still need review.</param>
public sealed record SendLatestOptions(string? ChannelId = null, long? FeedId = null, bool IncludeDisabled = false);
