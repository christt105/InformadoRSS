namespace InformadoRSS.Import;

/// <summary>The outcome for one MonitoRSS connection.</summary>
/// <param name="RouteId">The created or matched route; <c>null</c> when skipped or in a dry run that would create it.</param>
/// <param name="SourceChannelId">The Discord channel the connection posted to in MonitoRSS.</param>
/// <param name="ChannelId">The channel the route posts to: the source channel, or the test channel.</param>
/// <param name="Enabled">Whether the route is enabled after the import.</param>
/// <param name="NeedsReview">
/// Whether part of the connection could not be translated in a way that changes what gets posted, so the route was
/// imported disabled with a fallback (content only, no filter) for a human to fix and enable.
/// </param>
/// <param name="Notes">Everything that was translated with differences, dropped, or blocked the route.</param>
public sealed record RouteImportReport(
    string Name,
    RouteImportAction Action,
    long? RouteId,
    string? SourceChannelId,
    string? ChannelId,
    bool Enabled,
    bool NeedsReview,
    IReadOnlyList<string> Notes);
