using InformadoRSS.Core;

namespace InformadoRSS.Sources;

/// <summary>A parsed feed document.</summary>
public sealed record ParsedFeed(string? Title, IReadOnlyList<FeedItem> Items);
