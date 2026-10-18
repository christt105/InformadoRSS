namespace InformadoRSS.Core;

/// <summary>Where a source fetches its items from, plus the validators returned by the previous successful fetch.</summary>
public sealed record FeedDefinition(Uri Url, CacheValidators? Validators = null);
