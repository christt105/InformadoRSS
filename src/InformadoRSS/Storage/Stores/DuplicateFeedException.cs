namespace InformadoRSS.Storage;

/// <summary>Thrown when a feed URL is already used by another feed.</summary>
public sealed class DuplicateFeedException(Uri url)
    : Exception($"A feed with URL '{url}' already exists.")
{
    public Uri Url { get; } = url;
}
