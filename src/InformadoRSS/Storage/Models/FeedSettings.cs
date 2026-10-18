namespace InformadoRSS.Storage;

/// <summary>User-editable part of a feed.</summary>
public sealed record FeedSettings(Uri Url, string Name, TimeSpan Interval);
