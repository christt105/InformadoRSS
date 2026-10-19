using System.Text.RegularExpressions;
using InformadoRSS.Core;
using InformadoRSS.Filters;
using InformadoRSS.Scheduling;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Storage;

namespace InformadoRSS.Import;

/// <summary>
/// Sends the newest item that passes each route's filter, rendered with the route template, regardless of whether it was
/// seen. Meant for checking a migration by eye. Seen items, seeding and destination health are not touched.
/// </summary>
public sealed class LatestItemSender
{
    private readonly ISource _source;
    private readonly IFeedStore _feeds;
    private readonly IRouteStore _routes;
    private readonly IMessageRenderer _renderer;
    private readonly Dictionary<string, ISink> _sinks = new(StringComparer.Ordinal);

    public LatestItemSender(ISource source, IFeedStore feeds, IRouteStore routes, IMessageRenderer renderer, IEnumerable<ISink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _source = source;
        _feeds = feeds;
        _routes = routes;
        _renderer = renderer;
        foreach (var sink in sinks)
        {
            _sinks.TryAdd(sink.Name, sink);
        }
    }

    /// <exception cref="ArgumentException"><see cref="SendLatestOptions.ChannelId"/> is not a Discord id.</exception>
    public async Task<IReadOnlyList<SendLatestResult>> SendAsync(SendLatestOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ChannelId is not null && !SnowflakeComparer.IsSnowflake(options.ChannelId))
        {
            throw new ArgumentException($"Channel id \"{options.ChannelId}\" is not a Discord id.", nameof(options));
        }

        var results = new List<SendLatestResult>();
        foreach (var feed in await _feeds.ListAsync(cancellationToken))
        {
            if (options.FeedId is { } feedId && feed.Id != feedId)
            {
                continue;
            }

            var routes = (await _routes.ListByFeedAsync(feed.Id, cancellationToken))
                .Where(route => options.IncludeDisabled || route.Settings.Enabled)
                .ToList();
            if (routes.Count == 0)
            {
                continue;
            }

            var fetched = await _source.FetchAsync(new FeedDefinition(feed.Settings.Url), cancellationToken);
            foreach (var route in routes)
            {
                results.Add(fetched is FeedFetched { Items: var items }
                    ? await SendRouteAsync(feed, route, FeedProcessor.OldestFirst(items), options, cancellationToken)
                    : Result(feed, route, SendLatestStatus.FetchFailed, null, [fetched is FeedFetchFailed { Error: var error } ? error.Message : "no content"]));
            }
        }

        return results;
    }

    private async Task<SendLatestResult> SendRouteAsync(
        Feed feed, Storage.Route route, List<FeedItem> oldestFirst, SendLatestOptions options, CancellationToken cancellationToken)
    {
        var parsed = FilterJson.Parse(route.Settings.FilterJson);
        if (!parsed.IsValid)
        {
            return Result(feed, route, SendLatestStatus.InvalidFilter, null, [.. parsed.Errors.Select(error => error.ToString())]);
        }

        var filter = RouteFilter.Compile(parsed.Root);
        FeedItem? latest = null;
        for (var i = oldestFirst.Count - 1; i >= 0 && latest is null; i--)
        {
            try
            {
                if (filter.Matches(oldestFirst[i]))
                {
                    latest = oldestFirst[i];
                }
            }
            catch (RegexMatchTimeoutException)
            {
            }
        }

        if (latest is null)
        {
            return Result(feed, route, SendLatestStatus.NoMatchingItem, null, []);
        }

        var message = _renderer.Render(route.Settings.TemplateJson, latest);
        IReadOnlyList<Destination> destinations = options.ChannelId is { } channelId
            ? [new Destination(DiscordSink.SinkName, new Dictionary<string, string> { [DiscordSink.ChannelIdParameter] = channelId })]
            : route.Settings.Destinations;

        var errors = new List<string>();
        foreach (var destination in destinations)
        {
            if (!_sinks.TryGetValue(destination.Sink, out var sink))
            {
                errors.Add($"{destination.Sink}: no such sink");
                continue;
            }

            var result = await sink.DeliverAsync(destination, message, cancellationToken);
            if (result.Status != DeliveryStatus.Delivered)
            {
                errors.Add($"{destination.Sink}: {result.Status}: {result.Error}");
            }
        }

        return new SendLatestResult(
            feed.Id,
            feed.Settings.Name,
            route.Id,
            route.Settings.Name,
            errors.Count == 0 ? SendLatestStatus.Sent : SendLatestStatus.Failed,
            latest.Title,
            latest.Link,
            errors);
    }

    private static SendLatestResult Result(Feed feed, Storage.Route route, SendLatestStatus status, FeedItem? item, IReadOnlyList<string> errors) =>
        new(feed.Id, feed.Settings.Name, route.Id, route.Settings.Name, status, item?.Title, item?.Link, errors);
}
