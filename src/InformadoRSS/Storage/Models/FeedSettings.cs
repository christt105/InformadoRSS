namespace InformadoRSS.Storage;

/// <summary>User-editable part of a feed.</summary>
/// <param name="IdentityPattern">Optional regular expression over the item description whose match becomes part of the
/// item id, so a change in that part counts as a new item. See <see cref="Sources.ItemIdentity"/>.</param>
public sealed record FeedSettings(Uri Url, string Name, TimeSpan Interval, string? IdentityPattern = null);
