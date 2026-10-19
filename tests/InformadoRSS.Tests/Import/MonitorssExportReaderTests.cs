using System.Text;
using System.Text.Json;
using InformadoRSS.Import;

namespace InformadoRSS.Tests.Import;

public sealed class MonitorssExportReaderTests
{
    [Fact]
    public void ReadsEveryFeedAndConnectionOfTheExport()
    {
        var feeds = MonitorssFixture.Read();

        Assert.Equal(19, feeds.Count);
        Assert.Equal(22, feeds.Sum(feed => feed.Connections.Count));
        Assert.All(feeds, feed => Assert.Equal(600, feed.RefreshRateSeconds));
        Assert.All(feeds, feed => Assert.Equal(24, feed.Id!.Length));

        var blog = feeds[0];
        Assert.Equal("Blog de Christian", blog.Title);
        Assert.Equal("https://blog.example.com/index.xml", blog.Url);
        Assert.Contains("skips articles published more than 7 days ago (dateCheckOptions)", blog.UnsupportedOptions);
        Assert.Equal([MonitorssFixture.PanasGuild, MonitorssFixture.ZanahoriosGuild], blog.Connections.Select(c => c.GuildId));
        Assert.Equal("**{{title}}**\n{{link}}", blog.Connections[0].Content);
        Assert.Null(blog.Connections[0].FilterExpression);
        Assert.Empty(blog.Connections[0].IgnoredOptions);

        var changelog = feeds.Single(feed => feed.Title == "Obsidian Changelog");
        Assert.All(changelog.Connections, connection => Assert.NotNull(connection.FilterExpression));
    }

    [Fact]
    public void ReadsOneDocumentPerLineAndCanonicalNumbers()
    {
        var json = """
            {"_id":{"$oid":"6a0000000000000000000001"},"title":"A","url":"https://a.example/feed","refreshRateSeconds":{"$numberInt":"900"},"connections":{"discordChannels":[]}}
            {"_id":{"$oid":"6a0000000000000000000002"},"title":"B","url":"https://b.example/feed","refreshRateSeconds":600,"userRefreshRateSeconds":{"$numberLong":"1200"}}
            """;

        var feeds = MonitorssExportReader.Read(Encoding.UTF8.GetBytes(json));

        Assert.Equal(["A", "B"], feeds.Select(feed => feed.Title));
        Assert.Equal([900L, 1200L], feeds.Select(feed => feed.RefreshRateSeconds!.Value));
        Assert.Empty(feeds[1].Connections);
    }

    [Fact]
    public void ReportsOptionsThatCannotBeHonored()
    {
        var json = """
            [{"title":"A","url":"https://a.example/feed","disabledCode":"MANUAL","passingComparisons":["title"],
              "formatOptions":{"dateFormat":"DD/MM"},"externalProperties":[{"sourceField":"link","cssSelector":"p","label":"x"}],
              "connections":{"discordChannels":[{"id":{"$oid":"6a0000000000000000000009"},"name":"c","disabledCode":"BAD_FORMAT",
                "rateLimits":[{"timeWindowSeconds":60,"limit":1}],"customPlaceholders":[{"referenceName":"x"}],
                "mentions":{"targets":[{"id":"1","type":"role"}]},"splitOptions":{"isEnabled":true},
                "details":{"embeds":[{"title":"{{title}}"}],"componentRows":[{"id":"r","components":[]}],
                  "webhook":{"id":"5","token":"t","guildId":"7","channelId":"8"},"placeholderLimits":[{"placeholder":"title","characterCount":10}],
                  "formatter":{"stripImages":true,"ignoreNewLines":true},"forumThreadTitle":"{{title}}"}}]}}]
            """;

        var feed = Assert.Single(MonitorssExportReader.Read(Encoding.UTF8.GetBytes(json)));
        Assert.Equal("MANUAL", feed.DisabledCode);
        Assert.Equal(3, feed.UnsupportedOptions.Count);

        var connection = Assert.Single(feed.Connections);
        Assert.Equal("6a0000000000000000000009", connection.Id);
        Assert.Equal("BAD_FORMAT", connection.DisabledCode);
        Assert.Null(connection.ChannelId);
        Assert.Equal("8", connection.WebhookChannelId);
        Assert.Equal("7", connection.GuildId);
        Assert.Equal(1, connection.EmbedCount);
        Assert.True(connection.HasComponents);
        Assert.True(connection.HasThreadOptions);
        Assert.Equal(
            [
                "placeholder character limits", "formatter option stripImages", "formatter option ignoreNewLines",
                "custom placeholders", "1 mention target(s)", "custom rate limits", "message splitting",
            ],
            connection.IgnoredOptions);
    }

    [Fact]
    public void RejectsDocumentsWithoutUrl() =>
        Assert.Throws<JsonException>(() => MonitorssExportReader.Read("""[{"title":"A"}]"""u8));

    [Fact]
    public void RejectsInvalidJson() =>
        Assert.ThrowsAny<JsonException>(() => MonitorssExportReader.Read("[{"u8));
}
