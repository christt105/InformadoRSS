using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using InformadoRSS.Core;

namespace InformadoRSS.Sources;

/// <summary>A parsed feed document.</summary>
public sealed record ParsedFeed(string? Title, IReadOnlyList<FeedItem> Items);

/// <summary>The document is not well-formed XML or not an RSS or Atom feed.</summary>
public sealed class FeedParseException : Exception
{
    public FeedParseException()
    {
    }

    public FeedParseException(string message)
        : base(message)
    {
    }

    public FeedParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Parses RSS 2.0 (and 0.9x/1.0) and Atom documents into <see cref="FeedItem"/>s.</summary>
public static class FeedParser
{
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace RdfNs = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";

    private static readonly HashSet<XNamespace> MediaNamespaces =
    [
        "http://search.yahoo.com/mrss/",
        "http://search.yahoo.com/mrss",
    ];

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CheckCharacters = false,
    };

    /// <summary>Parses a feed document.</summary>
    /// <param name="baseUri">URL the document was fetched from, used to resolve relative links and images.</param>
    /// <exception cref="FeedParseException">The document is not well-formed XML or not an RSS or Atom feed.</exception>
    public static ParsedFeed Parse(Stream stream, Uri? baseUri = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(stream, ReaderSettings);
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new FeedParseException($"Invalid XML: {ex.Message}", ex);
        }

        var root = document.Root ?? throw new FeedParseException("The document has no root element.");

        return root.Name.LocalName switch
        {
            "rss" => ParseRss(root, baseUri),
            "RDF" when root.Name.Namespace == RdfNs => ParseRdf(root, baseUri),
            "feed" => ParseAtom(root, baseUri),
            _ => throw new FeedParseException($"Unsupported root element <{root.Name.LocalName}>; expected an RSS or Atom feed."),
        };
    }

    private static ParsedFeed ParseRss(XElement root, Uri? baseUri)
    {
        var channel = root.Element("channel") ?? throw new FeedParseException("RSS document without a channel element.");
        return ParseRssChannel(channel, channel, baseUri);
    }

    private static ParsedFeed ParseRssChannel(XElement channel, XElement itemContainer, Uri? baseUri)
    {
        var ns = channel.Name.Namespace;
        var items = itemContainer.Elements(ns + "item").Select(item => ParseRssItem(item, ns, baseUri)).ToList();
        return new ParsedFeed(TextOf(channel.Element(ns + "title")), items);
    }

    private static ParsedFeed ParseRdf(XElement root, Uri? baseUri)
    {
        var channel = root.Elements().FirstOrDefault(e => e.Name.LocalName == "channel")
            ?? throw new FeedParseException("RSS 1.0 document without a channel element.");
        return ParseRssChannel(channel, root, baseUri);
    }

    private static FeedItem ParseRssItem(XElement item, XNamespace ns, Uri? baseUri)
    {
        var guidElement = item.Element(ns + "guid");
        var guid = TextOf(guidElement);
        var guidIsPermaLink = !string.Equals(guidElement?.Attribute("isPermaLink")?.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase);

        var link = Resolve(TextOf(item.Element(ns + "link")), baseUri)
            ?? (guidIsPermaLink ? AbsoluteHttpUrl(guid) : null);

        var title = TextOf(item.Element(ns + "title")) is { } rawTitle ? LatinComposition.Compose(rawTitle) : null;
        var html = TextOf(item.Element(ns + "description")) ?? TextOf(item.Element(ContentNs + "encoded"));
        var (descriptionHtml, descriptionText) = html is not null
            ? (html, HtmlText.ToPlainText(html))
            : PlainDescription(FirstMedia(item, "description"));

        var author = TextOf(item.Element(DcNs + "creator")) ?? RssAuthorName(TextOf(item.Element(ns + "author")));
        var published = FeedDate.Parse(TextOf(item.Element(ns + "pubDate")) ?? TextOf(item.Element(DcNs + "date")));

        var categories = item.Elements(ns + "category")
            .Select(c => (Domain: NullIfEmpty(c.Attribute("domain")?.Value), Value: TextOf(c)))
            .Concat(item.Elements(DcNs + "subject").Select(c => (Domain: (string?)null, Value: TextOf(c))))
            .Where(c => c.Value is not null)
            .Select(c => new FeedCategory(c.Domain, c.Value!))
            .ToList();

        var enclosureImage = item.Elements(ns + "enclosure")
            .Where(e => IsImageType(e.Attribute("type")?.Value))
            .Select(e => e.Attribute("url")?.Value)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        var image = MediaImage(item) ?? enclosureImage ?? AbsoluteHttpUrl(TextOf(item.Element(ns + "image")));

        return new FeedItem(
            Id: guid ?? HashId(link, title, html),
            Title: title,
            Link: link,
            DescriptionHtml: descriptionHtml,
            DescriptionText: descriptionText,
            Author: author,
            Published: published,
            Categories: categories,
            ImageUrl: Resolve(image, baseUri));
    }

    private static ParsedFeed ParseAtom(XElement feed, Uri? baseUri)
    {
        var ns = feed.Name.Namespace;
        var feedAuthor = AtomAuthor(feed, ns);
        var items = feed.Elements(ns + "entry").Select(entry => ParseAtomEntry(entry, ns, feedAuthor, baseUri)).ToList();
        return new ParsedFeed(AtomText(feed.Element(ns + "title")).Text, items);
    }

    private static FeedItem ParseAtomEntry(XElement entry, XNamespace ns, string? feedAuthor, Uri? baseUri)
    {
        var links = entry.Elements(ns + "link").ToList();
        var link = links
            .Where(l => (l.Attribute("rel")?.Value ?? "alternate") == "alternate")
            .OrderBy(l => l.Attribute("type")?.Value is null or "text/html" ? 0 : 1)
            .Select(l => l.Attribute("href")?.Value)
            .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
        link = Resolve(link?.Trim(), baseUri);

        var title = AtomText(entry.Element(ns + "title")).Text;

        var summary = AtomText(entry.Element(ns + "summary"));
        var content = AtomText(entry.Element(ns + "content"));
        var description = summary.Html is not null ? summary : content;
        var (descriptionHtml, descriptionText) = description.Html is not null
            ? (description.Html, description.Text)
            : PlainDescription(FirstMedia(entry, "description"));

        var published = FeedDate.Parse(
            TextOf(entry.Element(ns + "published"))
            ?? TextOf(entry.Element(ns + "issued"))
            ?? TextOf(entry.Element(ns + "updated"))
            ?? TextOf(entry.Element(ns + "modified")));

        var categories = entry.Elements(ns + "category")
            .Select(c => (Domain: NullIfEmpty(c.Attribute("scheme")?.Value), Value: NullIfEmpty(c.Attribute("term")?.Value) ?? TextOf(c)))
            .Where(c => c.Value is not null)
            .Select(c => new FeedCategory(c.Domain, c.Value!))
            .ToList();

        var enclosureImage = links
            .Where(l => l.Attribute("rel")?.Value == "enclosure" && IsImageType(l.Attribute("type")?.Value))
            .Select(l => l.Attribute("href")?.Value)
            .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

        return new FeedItem(
            Id: TextOf(entry.Element(ns + "id")) ?? HashId(link, title, descriptionHtml),
            Title: title,
            Link: link,
            DescriptionHtml: descriptionHtml,
            DescriptionText: descriptionText,
            Author: AtomAuthor(entry, ns) ?? feedAuthor,
            Published: published,
            Categories: categories,
            ImageUrl: Resolve(MediaImage(entry) ?? enclosureImage, baseUri));
    }

    private static string? AtomAuthor(XElement parent, XNamespace ns)
    {
        var names = parent.Elements(ns + "author")
            .Select(a => TextOf(a.Element(ns + "name")) ?? TextOf(a.Element(ns + "email")))
            .OfType<string>()
            .ToList();
        return names.Count > 0 ? string.Join(", ", names) : null;
    }

    private static (string? Html, string? Text) AtomText(XElement? element)
    {
        if (element is null)
        {
            return (null, null);
        }

        var type = element.Attribute("type")?.Value.Trim().ToLowerInvariant();
        switch (type)
        {
            case "html" or "text/html":
            {
                var html = TextOf(element);
                return html is null ? (null, null) : (html, HtmlText.ToPlainText(html));
            }

            case "xhtml" or "application/xhtml+xml":
            {
                var container = element.Element(XhtmlNs + "div") ?? element;
                var html = string.Concat(container.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting))).Trim();
                return html.Length == 0 ? (null, null) : (html, HtmlText.ToPlainText(html));
            }

            default:
            {
                var text = TextOf(element);
                return text is null ? (null, null) : (HtmlText.FromPlainText(text), HtmlText.NormalizeText(text));
            }
        }
    }

    private static (string? Html, string? Text) PlainDescription(XElement? element)
    {
        var text = TextOf(element);
        if (text is null)
        {
            return (null, null);
        }

        return element!.Attribute("type")?.Value == "html"
            ? (text, HtmlText.ToPlainText(text))
            : (HtmlText.FromPlainText(text), HtmlText.NormalizeText(text));
    }

    private static XElement? FirstMedia(XElement item, string localName) =>
        MediaElements(item, localName).FirstOrDefault();

    private static IEnumerable<XElement> MediaElements(XElement item, string localName) =>
        item.Descendants().Where(e => e.Name.LocalName == localName && MediaNamespaces.Contains(e.Name.Namespace));

    private static string? MediaImage(XElement item)
    {
        var thumbnail = MediaElements(item, "thumbnail")
            .Select(e => e.Attribute("url")?.Value)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (thumbnail is not null)
        {
            return thumbnail.Trim();
        }

        return MediaElements(item, "content")
            .Where(e => e.Attribute("medium")?.Value == "image" || IsImageType(e.Attribute("type")?.Value))
            .Select(e => e.Attribute("url")?.Value)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
            ?.Trim();
    }

    private static bool IsImageType(string? mimeType) =>
        mimeType is not null && mimeType.TrimStart().StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string? RssAuthorName(string? author)
    {
        if (author is null)
        {
            return null;
        }

        var open = author.IndexOf('(', StringComparison.Ordinal);
        var close = author.LastIndexOf(')');
        if (open >= 0 && close > open && author[..open].Contains('@', StringComparison.Ordinal))
        {
            return NullIfEmpty(author[(open + 1)..close]) ?? author;
        }

        return author;
    }

    private static string HashId(string? link, string? title, string? description)
    {
        var source = link ?? $"{title}\n{description}";
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static string? Resolve(string? url, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = url.Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme != Uri.UriSchemeFile)
        {
            return url;
        }

        return baseUri is not null && Uri.TryCreate(baseUri, url, out var resolved) ? resolved.AbsoluteUri : url;
    }

    private static string? AbsoluteHttpUrl(string? value) =>
        value is not null && Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value
            : null;

    private static string? TextOf(XElement? element) => element is null ? null : NullIfEmpty(element.Value);

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
