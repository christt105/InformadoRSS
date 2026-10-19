namespace InformadoRSS.Import;

/// <summary>The outcome for one MonitoRSS feed and its connections.</summary>
/// <param name="FeedId">The created or merged feed; <c>null</c> when skipped or in a dry run that would create it.</param>
/// <param name="Notes">Feed options that could not be carried over.</param>
public sealed record FeedImportReport(
    string Name,
    string Url,
    FeedImportAction Action,
    long? FeedId,
    IReadOnlyList<string> Notes,
    IReadOnlyList<RouteImportReport> Routes);
