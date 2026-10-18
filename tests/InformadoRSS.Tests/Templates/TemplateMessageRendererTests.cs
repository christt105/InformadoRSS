using InformadoRSS.Core;
using InformadoRSS.Sources;
using InformadoRSS.Templates;

namespace InformadoRSS.Tests.Templates;

public sealed class TemplateMessageRendererTests
{
    private static readonly TemplateMessageRenderer Renderer = new();

    private static FeedItem Item(
        string? title = "Hello",
        string? link = "https://example.org/hello",
        string? description = null,
        string? author = null,
        DateTimeOffset? published = null,
        IReadOnlyList<FeedCategory>? categories = null,
        string? image = null) =>
        new("id-1", title, link, null, description, author, published, categories ?? [], image);

    private static FeedItem FixtureItem(string name, string baseUrl)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Sources", "Fixtures", name));
        return FeedParser.Parse(stream, new Uri(baseUrl)).Items[0];
    }

    [Fact]
    public void NullTemplateUsesMonitorRssDefault()
    {
        var message = Renderer.Render(null, Item());

        Assert.Equal("**Hello**\nhttps://example.org/hello", message.Content);
        Assert.Null(message.Embed);
    }

    [Fact]
    public void BoldTitleAndLinkTemplateOnBlogFixture()
    {
        var item = FixtureItem("christt105-blog.xml", "https://christt105.github.io/index.xml");

        var message = Renderer.Render("""{"content":"**{{title}}**\n{{link}}"}""", item);

        Assert.Equal(
            "**My relationship with mouth sores and vibecoding**\nhttps://christt105.github.io/blog/llagas-vibecoding/",
            message.Content);
    }

    [Fact]
    public void MyAnimeListTemplate()
    {
        var item = FixtureItem("mal-anime-oriorii.xml", "https://myanimelist.net/rss.php?type=rw&u=Oriorii");

        var message = Renderer.Render("""{"content":"Ori se ha visto **{{title}}**\n{{link}}"}""", item);

        Assert.Equal(
            "Ori se ha visto **Ranma ½ (2024) 3rd Season - TV**\nhttps://myanimelist.net/anime/63801/Ranma_½_2024_3rd_Season",
            message.Content);
    }

    [Fact]
    public void MediaTrackerEmbedWithThumbnailImageAndCategories()
    {
        var item = FixtureItem("media-tracker-finished.xml", "https://christt105.github.io/MediaTracker/finished.xml");
        const string Template = """
            {
              "content": "",
              "embed": {
                "title": "{{title}}",
                "url": "{{link}}",
                "description": "{{description}}",
                "color": 5793266,
                "image": "{{image}}",
                "footer": "{{category:type}} · {{category:status}}",
                "timestamp": true
              }
            }
            """;

        var message = Renderer.Render(Template, item);

        Assert.Equal(string.Empty, message.Content);
        var embed = Assert.IsType<RenderedEmbed>(message.Embed);
        Assert.Equal("Tetris", embed.Title);
        Assert.Equal("https://christt105.github.io/MediaTracker/movies/tetris-2023/", embed.Url);
        Assert.StartsWith("Sorprendentemente emocionante.", embed.Description, StringComparison.Ordinal);
        Assert.Equal(0x5865F2, embed.Color);
        Assert.Equal(
            "https://christt105.github.io/MediaTracker/movies/tetris-2023/tmdb_4F2QwCOYHJJjecSvdOjStuVLkpu_hu_3f763a9f4e8281c9.webp",
            embed.ImageUrl);
        Assert.Null(embed.ThumbnailUrl);
        Assert.Equal("movie · finished", embed.Footer);
        Assert.Equal(item.Published, embed.Timestamp);
    }

    [Fact]
    public void ExpandsEveryPlaceholder()
    {
        var item = Item(
            description: "Plain text",
            author: "Ori",
            published: new DateTimeOffset(2026, 10, 16, 7, 29, 0, TimeSpan.FromHours(-7)),
            categories: [new FeedCategory("type", "movie"), new FeedCategory(null, "misc")],
            image: "https://example.org/cover.webp");

        var message = Renderer.Render(
            """{"content":"{{ TITLE }}|{{link}}|{{author}}|{{description}}|{{image}}|{{published}}|{{category}}|{{category:TYPE}}"}""",
            item);

        Assert.Equal(
            "Hello|https://example.org/hello|Ori|Plain text|https://example.org/cover.webp|2026-10-16 14:29 UTC|movie, misc|movie",
            message.Content);
    }

    [Fact]
    public void UnknownAndMissingPlaceholdersBecomeEmpty()
    {
        var message = Renderer.Render("""{"content":"[{{feed}}][{{author}}][{{category:nope}}] {{ unclosed"}""", Item());

        Assert.Equal("[][][] {{ unclosed", message.Content);
    }

    [Fact]
    public void TruncatesContentWithEllipsis()
    {
        var message = Renderer.Render("""{"content":"{{description}}"}""", Item(description: new string('a', 5000)));

        Assert.Equal(MessageLimits.Content, message.Content.Length);
        Assert.EndsWith("a…", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatesEmbedFieldsAndTotal()
    {
        var item = Item(title: new string('t', 400), description: new string('d', 5000), author: new string('a', 400));
        const string Template = """
            {"embed":{"title":"{{title}}","description":"{{description}}","author":"{{author}}","footer":"{{description}}"}}
            """;

        var embed = Assert.IsType<RenderedEmbed>(Renderer.Render(Template, item).Embed);

        Assert.Equal(MessageLimits.EmbedTitle, embed.Title!.Length);
        Assert.Equal(MessageLimits.EmbedAuthor, embed.Author!.Length);
        Assert.Equal(MessageLimits.EmbedFooter, embed.Footer!.Length);
        Assert.EndsWith("…", embed.Description, StringComparison.Ordinal);
        Assert.Equal(
            MessageLimits.EmbedTotal,
            embed.Title.Length + embed.Description!.Length + embed.Footer.Length + embed.Author.Length);
    }

    [Fact]
    public void TruncationDoesNotSplitSurrogatePairs()
    {
        var text = new string('a', 9) + "\U0001F600" + "tail";

        var truncated = MessageLimits.Truncate(text, 11);

        Assert.Equal(new string('a', 9) + "…", truncated);
    }

    [Fact]
    public void DropsInvalidUrlsAndEmptyEmbed()
    {
        var message = Renderer.Render("""{"content":"{{title}}","embed":{"image":"{{image}}","url":"{{link}}"}}""", Item(link: "not a url"));

        Assert.Equal("Hello", message.Content);
        Assert.Null(message.Embed);
    }

    [Fact]
    public void EmptyResultFallsBackToDefault()
    {
        var message = Renderer.Render("""{"content":"{{author}}"}""", Item());

        Assert.Equal("**Hello**\nhttps://example.org/hello", message.Content);
    }

    [Fact]
    public void InvalidStoredTemplateFallsBackToDefault()
    {
        var message = Renderer.Render("{not json", Item());

        Assert.Equal("**Hello**\nhttps://example.org/hello", message.Content);
    }
}
