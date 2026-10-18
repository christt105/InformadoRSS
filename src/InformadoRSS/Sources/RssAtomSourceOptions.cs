using InformadoRSS.Core;

namespace InformadoRSS.Sources;

/// <summary>Limits applied by <see cref="RssAtomSource"/> to every fetch.</summary>
public sealed class RssAtomSourceOptions
{
    /// <summary>Maximum time for the whole request, including downloading the body.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Bodies larger than this are rejected with <see cref="FetchErrorKind.TooLarge"/>.</summary>
    public long MaxResponseBytes { get; init; } = 16 * 1024 * 1024;
}
