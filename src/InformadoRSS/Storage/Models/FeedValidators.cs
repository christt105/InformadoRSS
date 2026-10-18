namespace InformadoRSS.Storage;

/// <summary>Conditional request validators returned by the last successful fetch.</summary>
public sealed record FeedValidators(string? ETag, string? LastModified)
{
    public static FeedValidators None { get; } = new(null, null);
}
