using System.Net;
using System.Text;

namespace InformadoRSS.Sources;

/// <summary>Dependency-free conversion of feed HTML into readable plain text.</summary>
public static class HtmlText
{
    private static readonly HashSet<string> LineBreakTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "br", "dd", "div", "dl", "dt", "figcaption", "figure", "footer",
        "header", "li", "main", "nav", "section", "tbody", "td", "th", "thead", "tr",
    };

    private static readonly HashSet<string> ParagraphTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "blockquote", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "ol", "p", "pre", "table", "ul",
    };

    private static readonly HashSet<string> SkippedContentTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "noscript", "script", "style", "template", "title",
    };

    /// <summary>
    /// Removes tags, comments and the content of non-visible elements (<c>script</c>, <c>style</c>, <c>head</c>...),
    /// decodes entities, collapses whitespace, and turns block-level elements into line breaks.
    /// </summary>
    public static string ToPlainText(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var text = new StringBuilder(html.Length);
        var segment = new StringBuilder();
        var i = 0;

        while (i < html.Length)
        {
            var c = html[i];
            if (c != '<' || i + 1 >= html.Length || !IsMarkupStart(html[i + 1]))
            {
                segment.Append(c);
                i++;
                continue;
            }

            FlushSegment(text, segment);

            if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? html.Length : end + 3;
                continue;
            }

            var tagEnd = FindTagEnd(html, i + 1);
            var (name, closing) = ReadTagName(html, i + 1, tagEnd);
            i = tagEnd < 0 ? html.Length : tagEnd + 1;

            if (name.Length == 0)
            {
                continue;
            }

            if (!closing && SkippedContentTags.Contains(name))
            {
                i = SkipElementContent(html, i, name);
                continue;
            }

            if (ParagraphTags.Contains(name))
            {
                EnsureTrailingNewlines(text, 2);
            }
            else if (LineBreakTags.Contains(name))
            {
                EnsureTrailingNewlines(text, 1);
            }
        }

        FlushSegment(text, segment);
        return NormalizeLines(text.ToString());
    }

    /// <summary>Collapses runs of spaces and tabs, trims every line and keeps at most one empty line in a row.</summary>
    public static string NormalizeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))
        {
            if (c == '\n')
            {
                builder.Append('\n');
                pendingSpace = false;
            }
            else if (IsBlank(c))
            {
                pendingSpace = true;
            }
            else
            {
                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(c);
            }
        }

        return NormalizeLines(builder.ToString());
    }

    /// <summary>Escapes plain text so it can be embedded in HTML, turning line breaks into <c>&lt;br&gt;</c>.</summary>
    public static string FromPlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length + 16);
        foreach (var c in text.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            builder.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\n' => "<br>",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    private static bool IsBlank(char c) =>
        char.IsWhiteSpace(c) || c is '\u00AD' or '\u034F' or '\u200B' or '\u200C' or '\u2060' or '\uFEFF';

    private static bool IsMarkupStart(char c) => char.IsAsciiLetter(c) || c is '/' or '!' or '?';

    private static void FlushSegment(StringBuilder text, StringBuilder segment)
    {
        if (segment.Length == 0)
        {
            return;
        }

        var decoded = WebUtility.HtmlDecode(segment.ToString());
        segment.Clear();

        var pendingSpace = text.Length > 0 && text[^1] == ' ';
        foreach (var c in decoded)
        {
            if (IsBlank(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && text.Length > 0 && text[^1] is not (' ' or '\n'))
            {
                text.Append(' ');
            }

            pendingSpace = false;
            text.Append(c);
        }

        if (pendingSpace && text.Length > 0 && text[^1] is not (' ' or '\n'))
        {
            text.Append(' ');
        }
    }

    private static void EnsureTrailingNewlines(StringBuilder text, int count)
    {
        while (text.Length > 0 && text[^1] == ' ')
        {
            text.Length--;
        }

        var existing = 0;
        while (existing < text.Length && text[text.Length - 1 - existing] == '\n')
        {
            existing++;
        }

        text.Append('\n', Math.Max(0, count - existing));
    }

    private static int FindTagEnd(string html, int start)
    {
        var quote = '\0';
        for (var i = start; i < html.Length; i++)
        {
            var c = html[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static (string Name, bool Closing) ReadTagName(string html, int start, int end)
    {
        var limit = end < 0 ? html.Length : end;
        var closing = start < limit && html[start] == '/';
        var i = closing ? start + 1 : start;
        var nameStart = i;
        while (i < limit && (char.IsAsciiLetterOrDigit(html[i]) || html[i] is '-' or ':'))
        {
            i++;
        }

        return (html[nameStart..i], closing);
    }

    private static int SkipElementContent(string html, int start, string name)
    {
        var closingTag = "</" + name;
        var i = start;
        while (true)
        {
            var close = html.IndexOf(closingTag, i, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                return html.Length;
            }

            var after = close + closingTag.Length;
            if (after >= html.Length || !char.IsAsciiLetterOrDigit(html[after]))
            {
                var end = html.IndexOf('>', after);
                return end < 0 ? html.Length : end + 1;
            }

            i = after;
        }
    }

    private static string NormalizeLines(string text)
    {
        var builder = new StringBuilder(text.Length);
        var emptyLines = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                emptyLines++;
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(emptyLines > 0 ? "\n\n" : "\n");
            }

            builder.Append(line);
            emptyLines = 0;
        }

        return LatinComposition.Compose(builder.ToString());
    }
}
