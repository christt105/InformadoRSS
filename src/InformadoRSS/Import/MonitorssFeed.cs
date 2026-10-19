namespace InformadoRSS.Import;

/// <summary>The parts of a MonitoRSS <c>userfeeds</c> document that matter to the importer.</summary>
/// <param name="RefreshRateSeconds">The rate MonitoRSS applies: <c>userRefreshRateSeconds</c>, else <c>refreshRateSeconds</c>.</param>
/// <param name="UnsupportedOptions">Feed options that change MonitoRSS behavior and have no equivalent here.</param>
public sealed record MonitorssFeed(
    string? Id,
    string Title,
    string Url,
    long? RefreshRateSeconds,
    string? DisabledCode,
    IReadOnlyList<string> UnsupportedOptions,
    IReadOnlyList<MonitorssConnection> Connections);
