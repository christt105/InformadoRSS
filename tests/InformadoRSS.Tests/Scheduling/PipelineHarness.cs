using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using InformadoRSS.Storage;
using InformadoRSS.Tests.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InformadoRSS.Tests.Scheduling;

/// <summary>An in-memory database plus fake source, sinks and renderer wired into the real pipeline.</summary>
public sealed class PipelineHarness : IAsyncDisposable
{
    public static DateTimeOffset Start { get; } = new(2026, 10, 18, 4, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDatabase _db = new();
    private ServiceProvider? _services;

    public SchedulerOptions Options { get; set; } = new() { MaxItemAge = TimeSpan.FromDays(3650) };

    public ManualTimeProvider Time { get; } = new(Start);

    public DestinationBackoff Backoff { get; }

    public FakeSource Source { get; } = new();

    public FakeSink Discord { get; } = new("discord");

    public FakeSink Ntfy { get; } = new("ntfy");

    public FakeRenderer Renderer { get; } = new();

    /// <summary>Cycle observers registered in the scheduler services; add them before the first <see cref="CreateScheduler"/>.</summary>
    public List<IFeedCycleObserver> Observers { get; } = [];

    public IFeedStore Feeds => _db.Feeds;

    public IRouteStore Routes => _db.Routes;

    public ISeenItemStore SeenItems => _db.SeenItems;

    public ILatestItemStore LatestItems => _db.LatestItems;

    public SqliteDatabase Database => _db.Database;

    public static Destination DiscordChannel(string channelId) =>
        new("discord", new Dictionary<string, string> { ["channelId"] = channelId });

    public static Destination NtfyTopic(string topic) => new("ntfy", new Dictionary<string, string> { ["topic"] = topic });

    public static FeedItem Item(string id, DateTimeOffset? published = null, string? title = null) =>
        new(id, title ?? id, $"https://example.com/{id}", null, null, null, published, [], null);

    private PipelineHarness()
    {
        Backoff = new DestinationBackoff(Time);
    }

    public static async Task<PipelineHarness> CreateAsync()
    {
        var harness = new PipelineHarness();
        await harness._db.InitializeAsync();
        return harness;
    }

    public Task<Feed> AddFeedAsync(string name = "Feed", int intervalMinutes = 10) =>
        Feeds.AddAsync(
            new FeedSettings(new Uri($"https://example.com/{Uri.EscapeDataString(name)}.xml"), name, TimeSpan.FromMinutes(intervalMinutes)),
            TestContext.Current.CancellationToken);

    public async Task<Route> AddRouteAsync(Feed feed, string? filterJson = null, bool enabled = true, params Destination[] destinations)
    {
        var route = await Routes.AddAsync(
            feed.Id,
            new RouteSettings("Route", filterJson, null, enabled, destinations.Length == 0 ? [DiscordChannel("1")] : destinations),
            TestContext.Current.CancellationToken);
        return route!;
    }

    public async Task<Feed> ReloadAsync(Feed feed) => (await Feeds.GetAsync(feed.Id, TestContext.Current.CancellationToken))!;

    public FeedProcessor CreateProcessor() =>
        new(Source, Feeds, Routes, SeenItems, LatestItems, Renderer, [Discord, Ntfy], Backoff, Options, Time, NullLogger<FeedProcessor>.Instance);

    /// <summary>Reloads the feed from storage and runs one cycle, like the scheduler does.</summary>
    public async Task<FeedOutcome> RunCycleAsync(Feed feed) =>
        await CreateProcessor().ProcessAsync(await ReloadAsync(feed), TestContext.Current.CancellationToken);

    public FeedScheduler CreateScheduler()
    {
        if (_services is null)
        {
            var services = new ServiceCollection();
            foreach (var observer in Observers)
            {
                services.AddSingleton(observer);
            }

            _services = BuildServices(services);
        }

        return new FeedScheduler(
            Feeds, _services.GetRequiredService<IServiceScopeFactory>(), Options, Time, NullLogger<FeedScheduler>.Instance);
    }

    private ServiceProvider BuildServices(ServiceCollection services) =>
        services
            .AddSingleton<ISource>(Source)
            .AddSingleton(Feeds)
            .AddSingleton(Routes)
            .AddSingleton(SeenItems)
            .AddSingleton(LatestItems)
            .AddSingleton<IMessageRenderer, FakeRenderer>()
            .AddSingleton<ISink>(Discord)
            .AddSingleton<ISink>(Ntfy)
            .AddSingleton(Backoff)
            .AddSingleton(Options)
            .AddSingleton<TimeProvider>(Time)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddTransient<FeedProcessor>()
            .BuildServiceProvider();

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _db.DisposeAsync();
    }
}
