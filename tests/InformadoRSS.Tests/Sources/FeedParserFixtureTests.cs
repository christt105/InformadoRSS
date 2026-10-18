using InformadoRSS.Core;
using InformadoRSS.Sources;

namespace InformadoRSS.Tests.Sources;

public sealed class FeedParserFixtureTests
{
    private static ParsedFeed ParseFixture(string name, string baseUrl)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Sources", "Fixtures", name));
        return FeedParser.Parse(stream, new Uri(baseUrl));
    }

    [Fact]
    public void ParsesHugoBlog()
    {
        var feed = ParseFixture("christt105-blog.xml", "https://christt105.github.io/index.xml");

        Assert.Equal("About Me on Christian Martínez De La Rosa", feed.Title);
        Assert.Equal(3, feed.Items.Count);

        var item = feed.Items[0];
        Assert.Equal("https://christt105.github.io/blog/llagas-vibecoding/", item.Id);
        Assert.Equal("My relationship with mouth sores and vibecoding", item.Title);
        Assert.Equal("https://christt105.github.io/blog/llagas-vibecoding/", item.Link);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.FromHours(2)), item.Published);
        Assert.Equal("https://christt105.github.io/blog/llagas-vibecoding/cover.png", item.ImageUrl);
        Assert.Null(item.Author);
        Assert.Empty(item.Categories);
        Assert.StartsWith("<p>Hi again. Today&rsquo;s post", item.DescriptionHtml, StringComparison.Ordinal);
        Assert.StartsWith("Hi again. Today’s post is a bit different.", item.DescriptionText, StringComparison.Ordinal);
        Assert.DoesNotContain("<", item.DescriptionText, StringComparison.Ordinal);
        Assert.DoesNotContain("&rsquo;", item.DescriptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesMediaTrackerCategoriesWithDomainAndMediaImage()
    {
        var feed = ParseFixture("media-tracker-finished.xml", "https://christt105.github.io/MediaTracker/finished.xml");

        Assert.Equal("Acabados · Media Tracker", feed.Title);
        Assert.Equal(10, feed.Items.Count);

        var movie = feed.Items[0];
        Assert.Equal("Tetris", movie.Title);
        Assert.Equal(
            [new FeedCategory("status", "finished"), new FeedCategory("type", "movie")],
            movie.Categories);
        Assert.Equal(
            "https://christt105.github.io/MediaTracker/movies/tetris-2023/tmdb_4F2QwCOYHJJjecSvdOjStuVLkpu_hu_3f763a9f4e8281c9.webp",
            movie.ImageUrl);
        Assert.Equal(
            "Sorprendentemente emocionante. Desconocía los detalles de la historia y son bastante impactantes.",
            movie.DescriptionText);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero), movie.Published);

        var game = Assert.Single(feed.Items, i => i.Categories.Contains(new FeedCategory("type", "videogame")));
        Assert.Equal("The Legend of Zelda: The Wind Waker HD", game.Title);
        Assert.Single(feed.Items, i => i.Categories.Contains(new FeedCategory("type", "season")));
    }

    [Fact]
    public void ParsesYouTubeMediaGroup()
    {
        var feed = ParseFixture("youtube-alva-majo.xml", "https://www.youtube.com/feeds/videos.xml?channel_id=UCmaEoq1zaakpdudbzgll-zw");

        Assert.Equal("Alva Majo", feed.Title);
        Assert.Equal(15, feed.Items.Count);

        var video = feed.Items[0];
        Assert.Equal("yt:video:Y7ZZY4kA7Lo", video.Id);
        Assert.Equal("¿Cuánto dinero gané en 2025?", video.Title);
        Assert.Equal("https://www.youtube.com/watch?v=Y7ZZY4kA7Lo", video.Link);
        Assert.Equal("Alva Majo", video.Author);
        Assert.Equal(new DateTimeOffset(2026, 9, 2, 17, 0, 32, TimeSpan.Zero), video.Published);
        Assert.Equal("https://i2.ytimg.com/vi/Y7ZZY4kA7Lo/hqdefault.jpg", video.ImageUrl);
        Assert.StartsWith("ESJOY: https://r-bloop.cc/gaQGThL\nESJOY (México): ", video.DescriptionText, StringComparison.Ordinal);
        Assert.StartsWith("ESJOY: https://r-bloop.cc/gaQGThL<br>ESJOY (México): ", video.DescriptionHtml, StringComparison.Ordinal);
        Assert.All(feed.Items, i => Assert.StartsWith("yt:video:", i.Id, StringComparison.Ordinal));
        Assert.All(feed.Items, i => Assert.NotNull(i.ImageUrl));
    }

    [Theory]
    [InlineData("mal-anime-oriorii.xml", "https://myanimelist.net/rss.php?type=rw&u=Oriorii", "Oriorii's Anime from MyAnimeList.net")]
    [InlineData("mal-manga-polhostrex.xml", "https://myanimelist.net/rss.php?type=rm&u=PolHostRex", "PolHostRex's Manga from MyAnimeList.net")]
    public void ParsesMyAnimeList(string fixture, string url, string title)
    {
        var feed = ParseFixture(fixture, url);

        Assert.Equal(title, feed.Title);
        Assert.Equal(15, feed.Items.Count);
        Assert.All(feed.Items, i =>
        {
            Assert.Equal(i.Link, i.Id);
            Assert.StartsWith("https://myanimelist.net/", i.Link, StringComparison.Ordinal);
            Assert.NotNull(i.Published);
            Assert.Equal(TimeSpan.FromHours(-7), i.Published.Value.Offset);
            Assert.Equal(i.DescriptionHtml, i.DescriptionText);
        });
        Assert.Contains(feed.Items, i => i.DescriptionText!.Contains("Completed", StringComparison.Ordinal));
    }

    [Fact]
    public void ParsesMyAnimeListMangaItem()
    {
        var feed = ParseFixture("mal-manga-polhostrex.xml", "https://myanimelist.net/rss.php?type=rm&u=PolHostRex");

        var item = feed.Items[1];
        Assert.Equal("Oyasumi Punpun - Manga", item.Title);
        Assert.Equal("Completed - 147 of 147 chapters", item.DescriptionText);
        Assert.Equal(new DateTimeOffset(2024, 7, 10, 4, 50, 56, TimeSpan.FromHours(-7)), item.Published);
    }

    [Fact]
    public void ParsesKillTheNewsletterAtomWithHtmlContent()
    {
        var feed = ParseFixture("kill-the-newsletter-edicion-deluxe.xml", "https://kill-the-newsletter.com/feeds/p7dghlalanu4w02pkyr5.xml");

        Assert.Equal("Edición Deluxe", feed.Title);
        var entry = Assert.Single(feed.Items);
        Assert.Equal("urn:kill-the-newsletter:o5ygpr90nzxs31encdz23js1jfdol9twyf9ksjct", entry.Id);
        Assert.Equal("Crónica SAGA Barcelona Game Fest 2026", entry.Title);
        Assert.Equal(
            "https://kill-the-newsletter.com/feeds/p7dghlalanu4w02pkyr5/entries/o5ygpr90nzxs31encdz23js1jfdol9twyf9ksjct.html",
            entry.Link);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 10, 2, 25, 455, TimeSpan.Zero), entry.Published);
        Assert.Contains("<!doctype html>", entry.DescriptionHtml, StringComparison.Ordinal);
        Assert.StartsWith("Sábado 17 de octubre\n", entry.DescriptionText, StringComparison.Ordinal);
        Assert.Contains("Cada año voy al mismo evento de videojuegos y cada año lo disfruto más.", entry.DescriptionText, StringComparison.Ordinal);
        Assert.DoesNotContain("{", entry.DescriptionText, StringComparison.Ordinal);
        Assert.DoesNotContain("\u200C", entry.DescriptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesObsidianChangelog()
    {
        var feed = ParseFixture("obsidian-changelog.xml", "https://obsidian.md/changelog.xml");

        Assert.Equal("Obsidian Changelog", feed.Title);
        Assert.Equal(5, feed.Items.Count);

        var entry = feed.Items[0];
        Assert.Equal("https://obsidian.md/changelog/2026-10-05-mobile-v1.14.4/", entry.Id);
        Assert.Equal("Obsidian 1.14 Mobile (Public)", entry.Title);
        Assert.Equal("https://obsidian.md/changelog/2026-10-05-mobile-v1.14.4/", entry.Link);
        Assert.Equal("Obsidian", entry.Author);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero), entry.Published);
        Assert.StartsWith("Includes all new features and bug fixes up to Obsidian Desktop v1.14.4.\n\n", entry.DescriptionText, StringComparison.Ordinal);
        Assert.Contains("\n\nQuick Capture on iOS\n\n", entry.DescriptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesWordPressContentEncodedAndDcCreator()
    {
        var feed = ParseFixture("mixx-io.xml", "https://mixx.io/feed/");

        Assert.Equal(5, feed.Items.Count);
        var item = feed.Items[0];
        Assert.Equal("https://mixx.io/?p=19619", item.Id);
        Assert.Equal("https://mixx.io/2026/10/una-casita-hecha-de-manzanitas/", item.Link);
        Assert.Equal("Álex Barredo", item.Author);
        Assert.Equal([new FeedCategory(null, "Cupertino")], item.Categories);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 14, 20, 9, TimeSpan.Zero), item.Published);
        Assert.Contains("[…]", item.DescriptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesGodotItemImageAndCreator()
    {
        var feed = ParseFixture("godot-engine.xml", "https://godotengine.org/rss.xml");

        var item = feed.Items[0];
        Assert.Equal("Dev snapshot: Godot 4.8 beta 1", item.Title);
        Assert.Equal("Thaddeus Crews", item.Author);
        Assert.Equal("https://godotengine.org/storage/blog/covers/dev-snapshot-godot-4-8-beta-1.jpg", item.ImageUrl);
        Assert.Equal([new FeedCategory(null, "Pre-release")], item.Categories);
    }

    [Fact]
    public void AllFixturesProduceUniqueIdsAndTitles()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Sources", "Fixtures"), "*.xml"))
        {
            using var stream = File.OpenRead(file);
            var feed = FeedParser.Parse(stream);

            Assert.NotEmpty(feed.Items);
            Assert.Equal(feed.Items.Count, feed.Items.Select(i => i.Id).Distinct().Count());
            Assert.All(feed.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Title)));
            Assert.All(feed.Items, i => Assert.NotNull(i.Published));
        }
    }
}
