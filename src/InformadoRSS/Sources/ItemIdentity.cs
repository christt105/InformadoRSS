using System.Text.RegularExpressions;
using InformadoRSS.Core;

namespace InformadoRSS.Sources;

/// <summary>
/// Extends item ids with a part of their description, so a feed that keeps one id per entry and rewrites its
/// description (e.g. a MyAnimeList list moving an anime from <c>Watching</c> to <c>Completed</c>) produces a new item
/// whenever that part changes.
/// </summary>
public static class ItemIdentity
{
    public static TimeSpan RegexTimeout { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Builds the regular expression of an identity pattern.</summary>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    public static Regex CreateRegex(string pattern) => new(pattern, RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    /// Appends <c>#</c> and the first match of <paramref name="pattern"/> in the plain-text description to every item
    /// id. Items whose description does not match, or takes longer than <see cref="RegexTimeout"/>, keep their id.
    /// </summary>
    public static IReadOnlyList<FeedItem> Apply(IReadOnlyList<FeedItem> items, string? pattern)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (string.IsNullOrEmpty(pattern))
        {
            return items;
        }

        var regex = CreateRegex(pattern);
        return items.Select(item => item with { Id = IdOf(item, regex) }).ToList();
    }

    private static string IdOf(FeedItem item, Regex regex)
    {
        try
        {
            var match = regex.Match(item.DescriptionText ?? string.Empty);
            var key = match.Success ? match.Value.Trim() : string.Empty;
            return key.Length == 0 ? item.Id : item.Id + "#" + key;
        }
        catch (RegexMatchTimeoutException)
        {
            return item.Id;
        }
    }
}
