using System.Text.RegularExpressions;
using InformadoRSS.Core;
using InformadoRSS.Filters;
using InformadoRSS.Storage;
using Route = InformadoRSS.Storage.Route;

namespace InformadoRSS.Scheduling;

/// <summary>
/// Runs one cycle of a feed: fetches it, records the outcome, and pushes new items through every enabled route
/// (filter, dedupe, render, deliver). Not meant to run twice at the same time for the same feed.
/// </summary>
public sealed class FeedProcessor
{
    private readonly ISource _source;
    private readonly IFeedStore _feeds;
    private readonly IRouteStore _routes;
    private readonly ISeenItemStore _seenItems;
    private readonly IMessageRenderer _renderer;
    private readonly Dictionary<string, ISink> _sinks = new(StringComparer.Ordinal);
    private readonly SchedulerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<FeedProcessor> _logger;

    public FeedProcessor(
        ISource source,
        IFeedStore feeds,
        IRouteStore routes,
        ISeenItemStore seenItems,
        IMessageRenderer renderer,
        IEnumerable<ISink> sinks,
        SchedulerOptions options,
        TimeProvider time,
        ILogger<FeedProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _source = source;
        _feeds = feeds;
        _routes = routes;
        _seenItems = seenItems;
        _renderer = renderer;
        _options = options;
        _time = time;
        _logger = logger;
        foreach (var sink in sinks)
        {
            _sinks.TryAdd(sink.Name, sink);
        }
    }

    /// <summary>
    /// Processes the feed once. The validators of the response are only stored when every route finished its work,
    /// so items held back by the per-cycle cap or a temporary delivery failure are fetched again in full next time
    /// instead of hiding behind a <c>304 Not Modified</c>.
    /// </summary>
    public async Task<FeedOutcome> ProcessAsync(Feed feed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var routes = new List<(Route Route, bool Seeded)>();
        foreach (var route in await _routes.ListByFeedAsync(feed.Id, cancellationToken))
        {
            if (route.Settings.Enabled)
            {
                routes.Add((route, await _seenItems.HasAnyAsync(route.Id, cancellationToken)));
            }
        }

        var sendValidators = routes.TrueForAll(r => r.Seeded);
        var definition = new FeedDefinition(feed.Settings.Url, sendValidators ? ToCache(feed.Validators) : null);
        var result = await _source.FetchAsync(definition, cancellationToken);

        switch (result)
        {
            case FeedFetchFailed { Error: var error }:
                var failures = feed.Health.ConsecutiveFailures + 1;
                await _feeds.RecordFailureAsync(feed.Id, _time.GetUtcNow(), error.Message, cancellationToken);
                _logger.FetchFailed(feed.Id, feed.Settings.Name, error.Kind, error.StatusCode, failures, error.Message);
                return new FeedOutcome(false, failures, error.RetryAfter);

            case FeedNotModified notModified:
                await _feeds.RecordSuccessAsync(feed.Id, _time.GetUtcNow(), ToStorage(notModified.Validators), cancellationToken);
                return FeedOutcome.Success;

            case FeedFetched fetched:
                var complete = true;
                foreach (var (route, seeded) in routes)
                {
                    complete &= await ProcessRouteAsync(route, seeded, fetched.Items, cancellationToken);
                }

                var validators = complete ? ToStorage(fetched.Validators) : feed.Validators;
                await _feeds.RecordSuccessAsync(feed.Id, _time.GetUtcNow(), validators, cancellationToken);
                return FeedOutcome.Success;

            default:
                throw new InvalidOperationException($"Unsupported fetch result {result.GetType().Name}.");
        }
    }

    /// <returns>Whether the route has nothing left to do with these items.</returns>
    private async Task<bool> ProcessRouteAsync(Route route, bool seeded, IReadOnlyList<FeedItem> items, CancellationToken cancellationToken)
    {
        var name = route.Settings.Name;
        var ids = items.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();

        if (!seeded)
        {
            var count = await _seenItems.MarkSeenAsync(route.Id, ids, _time.GetUtcNow(), cancellationToken);
            _logger.RouteSeeded(route.Id, name, count);
            return true;
        }

        var parsed = FilterJson.Parse(route.Settings.FilterJson);
        if (!parsed.IsValid)
        {
            _logger.InvalidFilter(route.Id, name, string.Join("; ", parsed.Errors));
            return true;
        }

        var filter = RouteFilter.Compile(parsed.Root);
        var unseen = (await _seenItems.FilterUnseenAsync(route.Id, ids, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        if (unseen.Count == 0)
        {
            return true;
        }

        var matched = new List<FeedItem>();
        var rejected = new List<string>();
        foreach (var item in items)
        {
            if (!unseen.Remove(item.Id))
            {
                continue;
            }

            try
            {
                if (filter.Matches(item))
                {
                    matched.Add(item);
                }
                else
                {
                    rejected.Add(item.Id);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.FilterTimeout(route.Id, name, item.Id);
            }
        }

        if (rejected.Count > 0)
        {
            await _seenItems.MarkSeenAsync(route.Id, rejected, _time.GetUtcNow(), cancellationToken);
        }

        var ordered = OldestFirst(matched);
        var batch = ordered.Take(_options.MaxItemsPerRoute).ToList();
        var delivered = 0;
        foreach (var item in batch)
        {
            RenderedMessage message;
            try
            {
                message = _renderer.Render(route.Settings.TemplateJson, item);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.RenderFailed(ex, route.Id, name, item.Id);
                return false;
            }

            if (!await DeliverAsync(route, item, message, cancellationToken))
            {
                return false;
            }

            await _seenItems.MarkSeenAsync(route.Id, [item.Id], _time.GetUtcNow(), cancellationToken);
            delivered++;
        }

        var pending = ordered.Count - delivered;
        if (delivered > 0)
        {
            _logger.RouteDelivered(route.Id, name, delivered, pending);
        }

        return pending == 0;
    }

    /// <returns>Whether every destination is done with the item (delivered or permanently rejected).</returns>
    private async Task<bool> DeliverAsync(Route route, FeedItem item, RenderedMessage message, CancellationToken cancellationToken)
    {
        var destinations = route.Settings.Destinations;
        for (var position = 0; position < destinations.Count; position++)
        {
            var destination = destinations[position];
            if (!_sinks.TryGetValue(destination.Sink, out var sink))
            {
                _logger.UnknownSink(route.Id, route.Settings.Name, destination.Sink);
                return false;
            }

            DeliveryResult result;
            try
            {
                result = await sink.DeliverAsync(destination, message, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                result = DeliveryResult.Transient(ex.Message);
            }

            switch (result.Status)
            {
                case DeliveryStatus.Delivered:
                    await _routes.SetDestinationBrokenAsync(route.Id, position, _time.GetUtcNow(), null, cancellationToken);
                    break;
                case DeliveryStatus.Permanent:
                    _logger.DeliveryPermanent(route.Id, route.Settings.Name, item.Id, destination.Sink, position, result.Error);
                    await _routes.SetDestinationBrokenAsync(
                        route.Id, position, _time.GetUtcNow(), result.Error ?? "Rejected permanently", cancellationToken);
                    break;
                default:
                    _logger.DeliveryTransient(route.Id, route.Settings.Name, item.Id, destination.Sink, position, result.Error);
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Orders items by publication date when every item has one; otherwise assumes the usual newest-first document order
    /// and reverses it. The sort is stable, so items sharing a date keep the reversed document order.
    /// </summary>
    internal static List<FeedItem> OldestFirst(IReadOnlyList<FeedItem> items)
    {
        var reversed = items.Reverse().ToList();
        return reversed.TrueForAll(item => item.Published is not null)
            ? [.. reversed.OrderBy(item => item.Published)]
            : reversed;
    }

    private static CacheValidators? ToCache(FeedValidators validators) =>
        validators.ETag is null && validators.LastModified is null ? null : new CacheValidators(validators.ETag, validators.LastModified);

    private static FeedValidators ToStorage(CacheValidators validators) => new(validators.ETag, validators.LastModified);
}
