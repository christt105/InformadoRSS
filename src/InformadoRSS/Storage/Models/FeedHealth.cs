namespace InformadoRSS.Storage;

/// <summary>Fetch outcome tracking of a feed.</summary>
public sealed record FeedHealth(
    int ConsecutiveFailures,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastErrorAt,
    string? LastError)
{
    public static FeedHealth Unknown { get; } = new(0, null, null, null);
}
