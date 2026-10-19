using System.Text.RegularExpressions;
using InformadoRSS.Templates;

namespace InformadoRSS.Import;

/// <summary>
/// Translates the text content of a MonitoRSS connection into a route template. Placeholders with an equivalent are
/// rewritten; any other placeholder is removed and reported. Empty content becomes the default template, which is the
/// same <c>**{{title}}**</c> plus link layout.
/// </summary>
public static partial class MonitorssTemplateTranslator
{
    private static readonly Dictionary<string, string> Placeholders = new(StringComparer.Ordinal)
    {
        ["title"] = "title",
        ["link"] = "link",
        ["description"] = "description",
        ["author"] = "author",
        ["pubdate"] = "published",
        ["categories"] = "category",
    };

    private static readonly Dictionary<string, string> Differences = new(StringComparer.Ordinal)
    {
        ["description"] = "{{description}} is plain text here, MonitoRSS converted the HTML to Discord markdown",
        ["pubdate"] = "{{pubdate}} became {{published}}, always formatted as yyyy-MM-dd HH:mm UTC",
        ["categories"] = "{{categories}} became {{category}}, joined with commas",
    };

    public static TemplateTranslation Translate(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TemplateTranslation(null, []);
        }

        var notes = new List<string>();
        var translated = Token().Replace(content, match =>
        {
            var name = match.Groups["name"].Value;
            if (Placeholders.TryGetValue(name, out var ours))
            {
                if (Differences.TryGetValue(name, out var difference) && !notes.Contains(difference))
                {
                    notes.Add(difference);
                }

                return $"{{{{{ours}}}}}";
            }

            var removed = $"removed unsupported placeholder {match.Value}";
            if (!notes.Contains(removed))
            {
                notes.Add(removed);
            }

            return string.Empty;
        });

        if (string.IsNullOrWhiteSpace(translated))
        {
            notes.Add("nothing was left of the content, so the default template is used");
            return new TemplateTranslation(null, notes);
        }

        return new TemplateTranslation(new MessageTemplate(translated), notes);
    }

    [GeneratedRegex(@"\{\{\s*(?<name>[^{}]*?)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
