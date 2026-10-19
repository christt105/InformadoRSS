namespace InformadoRSS.Import;

/// <summary>Everything a MonitoRSS import did, or would do in a dry run.</summary>
/// <param name="TestChannelId">The channel every destination was redirected to, if any.</param>
/// <param name="TestGuildId">The guild stored with the redirected destinations, if any.</param>
public sealed record ImportReport(bool DryRun, string? TestChannelId, string? TestGuildId, IReadOnlyList<FeedImportReport> Feeds)
{
    public int FeedsCreated => Feeds.Count(feed => feed.Action == FeedImportAction.Created);

    public int FeedsMerged => Feeds.Count(feed => feed.Action == FeedImportAction.Merged);

    public int FeedsSkipped => Feeds.Count(feed => feed.Action == FeedImportAction.Skipped);

    public int RoutesCreated => CountRoutes(RouteImportAction.Created);

    public int RoutesRetargeted => CountRoutes(RouteImportAction.Retargeted);

    public int RoutesUnchanged => CountRoutes(RouteImportAction.Unchanged);

    public int RoutesSkipped => CountRoutes(RouteImportAction.Skipped);

    /// <summary>Routes imported disabled because something that changes what gets posted could not be translated.</summary>
    public int RoutesNeedingReview => Feeds.Sum(feed => feed.Routes.Count(route => route.NeedsReview));

    private int CountRoutes(RouteImportAction action) => Feeds.Sum(feed => feed.Routes.Count(route => route.Action == action));
}
