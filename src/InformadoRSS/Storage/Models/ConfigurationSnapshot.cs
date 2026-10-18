namespace InformadoRSS.Storage;

/// <summary>Portable copy of the whole configuration: feeds, routes, destinations and settings, without fetch state.</summary>
public sealed record ConfigurationSnapshot(
    int Version,
    IReadOnlyList<FeedSnapshot> Feeds,
    IReadOnlyDictionary<string, string> Settings)
{
    public const int CurrentVersion = 1;
}
