namespace InformadoRSS.Templates;

/// <summary>Discord's message limits, applied to every rendered message so it can be sent as is.</summary>
public static class MessageLimits
{
    public const int Content = 2000;
    public const int EmbedTitle = 256;
    public const int EmbedDescription = 4096;
    public const int EmbedFooter = 2048;
    public const int EmbedAuthor = 256;

    /// <summary>Combined length of the embed title, description, footer and author.</summary>
    public const int EmbedTotal = 6000;

    public const char Ellipsis = '…';

    /// <summary>
    /// Cuts <paramref name="text"/> to at most <paramref name="maxLength"/> UTF-16 units, ending in an ellipsis,
    /// without splitting a surrogate pair. Counting UTF-16 units is never more lenient than Discord's own count.
    /// </summary>
    public static string Truncate(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        if (text.Length <= maxLength)
        {
            return text;
        }

        var keep = maxLength - 1;
        if (keep > 0 && char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }

        return string.Concat(text.AsSpan(0, keep).TrimEnd(), Ellipsis.ToString());
    }
}
