using System.Text;
using InformadoRSS.Core;
using InformadoRSS.Sources;

namespace InformadoRSS.Tests.Sources;

public sealed class FeedParserTests
{
    private static ParsedFeed Parse(string xml, Uri? baseUri = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return FeedParser.Parse(stream, baseUri);
    }

    [Fact]
    public void ItemWithoutGuidGetsStableHashOfLink()
    {
        const string xml = """
            <rss version="2.0"><channel><title>T</title>
              <item><title>A</title><link>https://example.org/a</link></item>
            </channel></rss>
            """;

        var first = Parse(xml).Items[0];
        var second = Parse(xml.Replace("<title>A</title>", "<title>A (edited)</title>", StringComparison.Ordinal)).Items[0];

        Assert.StartsWith("sha256:", first.Id, StringComparison.Ordinal);
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Id, Parse(xml.Replace("/a<", "/b<", StringComparison.Ordinal)).Items[0].Id);
    }

    [Fact]
    public void PermalinkGuidIsUsedAsLinkFallback()
    {
        const string xml = """
            <rss version="2.0"><channel>
              <item><guid>https://example.org/post</guid><title>A</title></item>
              <item><guid isPermaLink="false">https://example.org/?p=1</guid><title>B</title></item>
            </channel></rss>
            """;

        var items = Parse(xml).Items;

        Assert.Equal("https://example.org/post", items[0].Link);
        Assert.Null(items[1].Link);
        Assert.Equal("https://example.org/?p=1", items[1].Id);
    }

    [Fact]
    public void RelativeLinksAndImagesAreResolvedAgainstTheFeedUrl()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <id>tag:example.org,2026:1</id>
                <title>A</title>
                <link rel="enclosure" type="image/jpeg" href="img/cover.jpg"/>
                <link href="/posts/1"/>
              </entry>
            </feed>
            """;

        var item = Parse(xml, new Uri("https://example.org/blog/feed.xml")).Items[0];

        Assert.Equal("https://example.org/posts/1", item.Link);
        Assert.Equal("https://example.org/blog/img/cover.jpg", item.ImageUrl);
    }

    [Fact]
    public void AtomPrefersAlternateHtmlLinkAndCategoryScheme()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <author><name>Feed Author</name></author>
              <entry>
                <id>1</id>
                <title type="html">Fish &amp;amp; Chips</title>
                <link rel="self" href="https://example.org/self"/>
                <link rel="alternate" type="application/json" href="https://example.org/1.json"/>
                <link rel="alternate" type="text/html" href="https://example.org/1"/>
                <category scheme="https://example.org/tags" term="news" label="News"/>
                <category term="misc"/>
                <updated>2026-01-02T03:04:05+01:00</updated>
                <summary>Plain &lt;summary&gt;</summary>
              </entry>
            </feed>
            """;

        var item = Parse(xml).Items[0];

        Assert.Equal("Fish & Chips", item.Title);
        Assert.Equal("https://example.org/1", item.Link);
        Assert.Equal("Feed Author", item.Author);
        Assert.Equal([new FeedCategory("https://example.org/tags", "news"), new FeedCategory(null, "misc")], item.Categories);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(1)), item.Published);
        Assert.Equal("Plain <summary>", item.DescriptionText);
        Assert.Equal("Plain &lt;summary&gt;", item.DescriptionHtml);
    }

    [Fact]
    public void AtomXhtmlContentIsSerializedAsHtml()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <id>1</id><title>A</title>
                <content type="xhtml"><div xmlns="http://www.w3.org/1999/xhtml"><p>Hello <b>world</b></p></div></content>
              </entry>
            </feed>
            """;

        var item = Parse(xml).Items[0];

        Assert.Contains("Hello", item.DescriptionHtml, StringComparison.Ordinal);
        Assert.Contains("<b", item.DescriptionHtml, StringComparison.Ordinal);
        Assert.Equal("Hello world", item.DescriptionText);
    }

    [Fact]
    public void RssAuthorEmailIsReducedToName()
    {
        const string xml = """
            <rss version="2.0"><channel>
              <item><guid>1</guid><author>jane@example.org (Jane Doe)</author></item>
              <item><guid>2</guid><author>john@example.org</author></item>
            </channel></rss>
            """;

        var items = Parse(xml).Items;

        Assert.Equal("Jane Doe", items[0].Author);
        Assert.Equal("john@example.org", items[1].Author);
    }

    [Fact]
    public void MediaContentImageIsUsedWhenThereIsNoThumbnail()
    {
        const string xml = """
            <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/"><channel>
              <item>
                <guid>1</guid>
                <media:content url="https://example.org/video.mp4" type="video/mp4"/>
                <media:content url="https://example.org/photo.jpg" medium="image"/>
                <enclosure url="https://example.org/enclosure.png" type="image/png" length="1"/>
              </item>
              <item>
                <guid>2</guid>
                <enclosure url="https://example.org/episode.mp3" type="audio/mpeg" length="1"/>
                <enclosure url="https://example.org/enclosure.png" type="image/png" length="1"/>
              </item>
            </channel></rss>
            """;

        var items = Parse(xml).Items;

        Assert.Equal("https://example.org/photo.jpg", items[0].ImageUrl);
        Assert.Equal("https://example.org/enclosure.png", items[1].ImageUrl);
    }

    [Fact]
    public void ParsesRss10()
    {
        const string xml = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns="http://purl.org/rss/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/">
              <channel rdf:about="https://example.org/"><title>RDF feed</title></channel>
              <item rdf:about="https://example.org/1">
                <title>One</title><link>https://example.org/1</link>
                <dc:date>2026-03-04T05:06:07Z</dc:date><dc:creator>Ann</dc:creator><dc:subject>tag</dc:subject>
              </item>
            </rdf:RDF>
            """;

        var feed = Parse(xml);

        Assert.Equal("RDF feed", feed.Title);
        var item = Assert.Single(feed.Items);
        Assert.Equal("https://example.org/1", item.Link);
        Assert.Equal("Ann", item.Author);
        Assert.Equal([new FeedCategory(null, "tag")], item.Categories);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), item.Published);
    }

    [Fact]
    public void DoctypeIsIgnoredAndEntitiesAreNotExpanded()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
            <rss version="2.0"><channel><title>T</title><item><guid>1</guid><title>A</title></item></channel></rss>
            """;

        var feed = Parse(xml);

        Assert.Equal("A", Assert.Single(feed.Items).Title);
    }

    [Theory]
    [InlineData("<html><body>Not a feed</body></html>")]
    [InlineData("<rss version=\"2.0\"></rss>")]
    [InlineData("<rss><channel><item>")]
    [InlineData("")]
    public void InvalidDocumentsThrowFeedParseException(string xml)
    {
        Assert.Throws<FeedParseException>(() => Parse(xml));
    }
}
