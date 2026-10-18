using System.Globalization;
using System.Text;
using InformadoRSS.Core;

namespace InformadoRSS.Templates;

/// <summary>
/// Expands <c>{{name}}</c> tokens with values from a <see cref="FeedItem"/>. Names are case-insensitive and may be
/// surrounded by spaces. Unknown or missing values expand to an empty string; an unclosed <c>{{</c> is kept as text.
/// </summary>
public static class TemplatePlaceholders
{
    private const string Open = "{{";
    private const string Close = "}}";
    private const string CategoryName = "category";

    /// <summary>Every supported token, for help texts and error messages.</summary>
    public static IReadOnlyList<string> Supported { get; } =
    [
        "{{title}}", "{{link}}", "{{author}}", "{{description}}", "{{image}}", "{{published}}",
        "{{category}}", "{{category:<domain>}}",
    ];

    private static readonly HashSet<string> SimpleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "link", "author", "description", "image", "published", CategoryName,
    };

    public static string Expand(string template, FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(item);

        if (!template.Contains(Open, StringComparison.Ordinal))
        {
            return template;
        }

        var builder = new StringBuilder(template.Length + 64);
        var position = 0;
        foreach (var token in Tokens(template))
        {
            builder.Append(template, position, token.Start - position);
            builder.Append(Value(token.Name, token.Argument, item));
            position = token.End;
        }

        builder.Append(template, position, template.Length - position);
        return builder.ToString();
    }

    /// <summary>Returns the tokens of <paramref name="template"/> that are not supported, as written.</summary>
    public static IReadOnlyList<string> FindUnknown(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        List<string> unknown = [];
        foreach (var token in Tokens(template))
        {
            if (!IsKnown(token.Name, token.Argument))
            {
                unknown.Add(template[token.Start..token.End]);
            }
        }

        return unknown;
    }

    /// <summary>Whether <paramref name="text"/> contains at least one token, known or not.</summary>
    public static bool ContainsToken(string text) => Tokens(text).Any();

    private static bool IsKnown(string name, string? argument) =>
        argument is null
            ? SimpleNames.Contains(name)
            : name.Equals(CategoryName, StringComparison.OrdinalIgnoreCase) && argument.Length > 0;

    private static string Value(string name, string? argument, FeedItem item)
    {
        if (!IsKnown(name, argument))
        {
            return string.Empty;
        }

        return name.ToLowerInvariant() switch
        {
            "title" => item.Title ?? string.Empty,
            "link" => item.Link ?? string.Empty,
            "author" => item.Author ?? string.Empty,
            "description" => item.DescriptionText ?? string.Empty,
            "image" => item.ImageUrl ?? string.Empty,
            "published" => item.Published?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                ?? string.Empty,
            CategoryName => string.Join(", ", item.Categories
                .Where(category => argument is null || string.Equals(category.Domain, argument, StringComparison.OrdinalIgnoreCase))
                .Select(category => category.Value)),
            _ => string.Empty,
        };
    }

    private static IEnumerable<Token> Tokens(string template)
    {
        var index = 0;
        while (true)
        {
            var start = template.IndexOf(Open, index, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            var close = template.IndexOf(Close, start + Open.Length, StringComparison.Ordinal);
            if (close < 0)
            {
                yield break;
            }

            var nested = template.IndexOf(Open, start + Open.Length, close - start - Open.Length, StringComparison.Ordinal);
            if (nested >= 0)
            {
                index = nested;
                continue;
            }

            var inner = template[(start + Open.Length)..close].Trim();
            var colon = inner.IndexOf(':', StringComparison.Ordinal);
            var name = colon < 0 ? inner : inner[..colon].TrimEnd();
            var argument = colon < 0 ? null : inner[(colon + 1)..].TrimStart();
            var end = close + Close.Length;
            yield return new Token(start, end, name, argument);
            index = end;
        }
    }

    private readonly record struct Token(int Start, int End, string Name, string? Argument);
}
