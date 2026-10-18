using InformadoRSS.Core;

namespace InformadoRSS.Storage;

/// <summary>User-editable part of a feed.</summary>
public sealed record FeedSettings(Uri Url, string Name, TimeSpan Interval);

/// <summary>Fetch outcome tracking of a feed.</summary>
public sealed record FeedHealth(
    int ConsecutiveFailures,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastErrorAt,
    string? LastError)
{
    public static FeedHealth Unknown { get; } = new(0, null, null, null);
}

/// <summary>Conditional request validators returned by the last successful fetch.</summary>
public sealed record FeedValidators(string? ETag, string? LastModified)
{
    public static FeedValidators None { get; } = new(null, null);
}

public sealed record Feed(long Id, FeedSettings Settings, FeedHealth Health, FeedValidators Validators);

/// <summary>
/// User-editable part of a route. <paramref name="FilterJson"/> and <paramref name="TemplateJson"/> are opaque JSON
/// documents owned by the filter and template engines; <c>null</c> means "match everything" and "default template".
/// </summary>
public sealed record RouteSettings(
    string Name,
    string? FilterJson,
    string? TemplateJson,
    bool Enabled,
    IReadOnlyList<Destination> Destinations);

public sealed record Route(long Id, long FeedId, RouteSettings Settings);
