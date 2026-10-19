using System.Text;
using InformadoRSS.Filters;
using InformadoRSS.Import;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Storage;
using InformadoRSS.Templates;
using InformadoRSS.Tests.Scheduling;
using static InformadoRSS.Tests.Scheduling.PipelineHarness;

namespace InformadoRSS.Tests.Import;

public sealed class MonitorssImporterTests : IAsyncLifetime
{
    private const string TestChannel = "900000000000000001";
    private const string TestGuild = "900000000000000002";

    private PipelineHarness _h = null!;

    public async ValueTask InitializeAsync() => _h = await CreateAsync();

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    private MonitorssImporter Importer => new(_h.Feeds, _h.Routes);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<ImportReport> ImportAsync(ImportOptions? options = null) =>
        Importer.ImportAsync(MonitorssFixture.Read(), options ?? new ImportOptions(), Ct);

    private async Task<List<Route>> AllRoutesAsync()
    {
        var routes = new List<Route>();
        foreach (var feed in await _h.Feeds.ListAsync(Ct))
        {
            routes.AddRange(await _h.Routes.ListByFeedAsync(feed.Id, Ct));
        }

        return routes;
    }

    [Fact]
    public async Task ImportsTheWholeInventoryEnabledWithoutReview()
    {
        var report = await ImportAsync();

        Assert.Equal(19, report.FeedsCreated);
        Assert.Equal(22, report.RoutesCreated);
        Assert.Equal(0, report.RoutesNeedingReview);
        Assert.All(report.Feeds.SelectMany(feed => feed.Routes), route => Assert.True(route.Enabled));

        var feeds = await _h.Feeds.ListAsync(Ct);
        Assert.Equal(19, feeds.Count);
        Assert.All(feeds, feed => Assert.Equal(TimeSpan.FromSeconds(600), feed.Settings.Interval));

        var routes = await AllRoutesAsync();
        Assert.Equal(22, routes.Count);
        Assert.All(routes, route => Assert.Equal(DiscordSink.SinkName, Assert.Single(route.Settings.Destinations).Sink));
        Assert.All(routes, route => Assert.True(route.Settings.Destinations[0].Parameters.ContainsKey(MonitorssImporter.ConnectionIdParameter)));
    }

    [Fact]
    public async Task TranslatesTemplatesFiltersAndDestinations()
    {
        await ImportAsync();
        var feeds = await _h.Feeds.ListAsync(Ct);

        var blog = feeds.Single(feed => feed.Settings.Name == "Blog de Christian");
        var blogRoutes = await _h.Routes.ListByFeedAsync(blog.Id, Ct);
        Assert.Equal(2, blogRoutes.Count);
        Assert.Equal("**{{title}}**\n{{link}}", TemplateJson.Parse(blogRoutes[0].Settings.TemplateJson).Template!.Content);
        Assert.Null(blogRoutes[0].Settings.FilterJson);
        Assert.Equal(
            [MonitorssFixture.PanasGuild, MonitorssFixture.ZanahoriosGuild],
            blogRoutes.Select(route => route.Settings.Destinations[0].Parameters[DiscordSink.GuildIdParameter]));

        var godot = await _h.Routes.ListByFeedAsync(feeds.Single(feed => feed.Settings.Name == "Godot Engine Official").Id, Ct);
        Assert.Null(Assert.Single(godot).Settings.TemplateJson);

        var mal = Assert.Single(await _h.Routes.ListByFeedAsync(feeds.Single(feed => feed.Settings.Name == "MAL anime de Ori").Id, Ct));
        var malFilter = Assert.IsType<FilterCondition>(Assert.Single(Assert.IsType<AllGroup>(FilterJson.Parse(mal.Settings.FilterJson).Root).Children));
        Assert.Equal((FilterField.Description, FilterOperator.Contains, "Completed", false), (malFilter.Field, malFilter.Operator, malFilter.Value, malFilter.Negate));
        Assert.Equal("Ori se ha visto **{{title}}**\n{{link}}", TemplateJson.Parse(mal.Settings.TemplateJson).Template!.Content);

        var changelog = await _h.Routes.ListByFeedAsync(feeds.Single(feed => feed.Settings.Name == "Obsidian Changelog").Id, Ct);
        Assert.All(changelog, route =>
        {
            var condition = Assert.IsType<FilterCondition>(Assert.Single(Assert.IsType<AllGroup>(FilterJson.Parse(route.Settings.FilterJson).Root).Children));
            Assert.True(condition.Negate);
            Assert.Equal("Early access", condition.Value);
        });
    }

    [Fact]
    public async Task ReportsWhatCouldNotBeCarriedOver()
    {
        var report = await ImportAsync(new ImportOptions(DryRun: true));

        Assert.All(report.Feeds, feed => Assert.Contains("skips articles published more than 7 days ago (dateCheckOptions)", feed.Notes));
        Assert.All(report.Feeds.SelectMany(feed => feed.Routes), route => Assert.DoesNotContain(route.Notes, note => note.StartsWith("ignored", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ReRunningDoesNotDuplicateAnything()
    {
        await ImportAsync();
        var again = await ImportAsync();

        Assert.Equal(0, again.FeedsCreated);
        Assert.Equal(19, again.FeedsMerged);
        Assert.Equal(0, again.RoutesCreated);
        Assert.Equal(22, again.RoutesUnchanged);
        Assert.Equal(22, (await AllRoutesAsync()).Count);
    }

    [Fact]
    public async Task TestImportRedirectsEverythingAndRealImportMovesItBackKeepingSeenItems()
    {
        var test = await ImportAsync(new ImportOptions(TestChannel, TestGuildId: TestGuild));

        Assert.Equal(22, test.RoutesCreated);
        var routes = await AllRoutesAsync();
        Assert.All(routes, route =>
        {
            var parameters = route.Settings.Destinations[0].Parameters;
            Assert.Equal(TestChannel, parameters[DiscordSink.ChannelIdParameter]);
            Assert.Equal(TestGuild, parameters[DiscordSink.GuildIdParameter]);
        });

        var seeded = routes[0];
        await _h.SeenItems.SeedAsync(seeded.Id, ["a"], Start, Ct);

        var real = await ImportAsync();

        Assert.Equal(22, real.RoutesRetargeted);
        Assert.Equal(0, real.RoutesCreated);
        var after = await AllRoutesAsync();
        Assert.Equal(22, after.Count);
        Assert.All(after, route =>
        {
            var parameters = route.Settings.Destinations[0].Parameters;
            Assert.NotEqual(TestChannel, parameters[DiscordSink.ChannelIdParameter]);
            Assert.Contains(parameters[DiscordSink.GuildIdParameter], new[] { MonitorssFixture.PanasGuild, MonitorssFixture.ZanahoriosGuild });
        });
        Assert.True(await _h.SeenItems.IsSeededAsync(seeded.Id, Ct));
        Assert.Empty(await _h.SeenItems.FilterUnseenAsync(seeded.Id, ["a"], Ct));
    }

    [Fact]
    public async Task TestImportWithoutGuildDropsTheOriginalGuild()
    {
        await ImportAsync(new ImportOptions(TestChannel));

        Assert.All(await AllRoutesAsync(), route =>
            Assert.False(route.Settings.Destinations[0].Parameters.ContainsKey(DiscordSink.GuildIdParameter)));
    }

    [Fact]
    public async Task DryRunWritesNothing()
    {
        var report = await ImportAsync(new ImportOptions(DryRun: true));

        Assert.True(report.DryRun);
        Assert.Equal(19, report.FeedsCreated);
        Assert.Equal(22, report.RoutesCreated);
        Assert.Empty(await _h.Feeds.ListAsync(Ct));
    }

    [Fact]
    public async Task RejectsInvalidTestIds()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ImportAsync(new ImportOptions("abc")));
        await Assert.ThrowsAsync<ArgumentException>(() => ImportAsync(new ImportOptions(TestGuildId: TestGuild)));
    }

    [Fact]
    public async Task ImportedRoutesAreSeededByTheFirstFetchWithoutSending()
    {
        await ImportAsync();
        var feed = (await _h.Feeds.ListAsync(Ct)).Single(f => f.Settings.Name == "Godot Engine Official");
        var route = Assert.Single(await _h.Routes.ListByFeedAsync(feed.Id, Ct));
        Assert.False(await _h.SeenItems.IsSeededAsync(route.Id, Ct));

        _h.Source.ReturnsItems(Item("b"), Item("a"));
        await _h.RunCycleAsync(feed);

        Assert.Empty(_h.Discord.Attempts);
        Assert.True(await _h.SeenItems.IsSeededAsync(route.Id, Ct));

        _h.Source.ReturnsItems(FakeSource.V2, Item("c"), Item("b"), Item("a"));
        await _h.RunCycleAsync(feed);
        Assert.Equal(["c"], _h.Discord.Contents);
    }

    [Fact]
    public async Task MergesFeedsSharingAUrlAndKeepsExistingManualRoutes()
    {
        var existing = await _h.Feeds.AddAsync(new FeedSettings(new Uri("https://a.example/feed"), "Mine", TimeSpan.FromMinutes(5)), Ct);
        await _h.Routes.AddAsync(existing.Id, new RouteSettings("manual", null, null, true, [DiscordChannel("11")]), Ct);

        var json = """
            [{"_id":{"$oid":"6a0000000000000000000001"},"title":"A","url":"https://a.example/feed","refreshRateSeconds":600,
              "connections":{"discordChannels":[{"id":{"$oid":"6a00000000000000000000a1"},"name":"same","details":{"channel":{"id":"11","guildId":"1"}}}]}},
             {"_id":{"$oid":"6a0000000000000000000002"},"title":"A again","url":"https://a.example/feed","refreshRateSeconds":600,
              "connections":{"discordChannels":[{"id":{"$oid":"6a00000000000000000000a2"},"name":"other","details":{"channel":{"id":"12","guildId":"1"}}}]}}]
            """;
        var report = await Importer.ImportAsync(MonitorssExportReader.Read(Encoding.UTF8.GetBytes(json)), new ImportOptions(), Ct);

        Assert.Equal(2, report.FeedsMerged);
        Assert.Equal(RouteImportAction.Unchanged, report.Feeds[0].Routes[0].Action);
        Assert.Equal(RouteImportAction.Created, report.Feeds[1].Routes[0].Action);
        Assert.Contains(report.Feeds[0].Notes, note => note.StartsWith("kept the existing feed", StringComparison.Ordinal));
        Assert.Single(await _h.Feeds.ListAsync(Ct));
        Assert.Equal(["manual", "other"], (await _h.Routes.ListByFeedAsync(existing.Id, Ct)).Select(route => route.Settings.Name));
    }

    [Fact]
    public async Task UntranslatableConnectionsAreImportedDisabledOrSkipped()
    {
        var json = """
            [{"title":"A","url":"https://a.example/feed",
              "connections":{"discordChannels":[
                {"id":{"$oid":"6a00000000000000000000b1"},"name":"embed","details":{"channel":{"id":"21","guildId":"1"},"content":"","embeds":[{"title":"{{title}}"}]}},
                {"id":{"$oid":"6a00000000000000000000b2"},"name":"filter","details":{"channel":{"id":"22","guildId":"1"}},
                 "filters":{"expression":{"type":"RELATIONAL","op":"CONTAINS","left":{"type":"ARTICLE","value":"summary"},"right":{"type":"STRING","value":"x"}}}},
                {"id":{"$oid":"6a00000000000000000000b3"},"name":"paused","disabledCode":"MANUAL","details":{"channel":{"id":"23","guildId":"1"}}},
                {"id":{"$oid":"6a00000000000000000000b4"},"name":"nowhere","details":{}}]}}]
            """;
        var report = await Importer.ImportAsync(MonitorssExportReader.Read(Encoding.UTF8.GetBytes(json)), new ImportOptions(), Ct);
        var routes = report.Feeds[0].Routes;

        Assert.Equal([true, true, false, false], routes.Select(route => route.NeedsReview));
        Assert.Equal([false, false, false, false], routes.Select(route => route.Enabled));
        Assert.Equal(RouteImportAction.Skipped, routes[3].Action);
        Assert.Equal(2, report.RoutesNeedingReview);

        var stored = await _h.Routes.ListByFeedAsync(report.Feeds[0].FeedId!.Value, Ct);
        Assert.Equal(3, stored.Count);
        Assert.Null(stored[0].Settings.TemplateJson);
        Assert.Null(stored[1].Settings.FilterJson);
    }
}
