using InformadoRSS.Storage;

namespace InformadoRSS.Scheduling;

/// <summary>
/// Decides which feeds are due and runs a <see cref="FeedProcessor"/> for each of them, never two at the same time for
/// the same feed and at most <see cref="SchedulerOptions.MaxConcurrency"/> overall. Due times live in memory; after a
/// restart they are rebuilt from the last success or failure stored with each feed. Due times follow the current feed
/// interval, so an interval change applies from the next <see cref="StartDueAsync"/> on. Every registered
/// <see cref="IFeedCycleObserver"/> is notified after each completed cycle.
/// </summary>
public sealed class FeedScheduler : IDisposable
{
    private readonly IFeedStore _feeds;
    private readonly IServiceScopeFactory _scopes;
    private readonly SchedulerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<FeedScheduler> _logger;
    private readonly SemaphoreSlim _slots;
    private readonly Lock _lock = new();
    private readonly Dictionary<long, Schedule> _schedules = [];
    private readonly HashSet<long> _running = [];
    private readonly List<Task> _tasks = [];

    public FeedScheduler(
        IFeedStore feeds, IServiceScopeFactory scopes, SchedulerOptions options, TimeProvider time, ILogger<FeedScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _feeds = feeds;
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
        _slots = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
    }

    /// <summary>Starts processing every due feed that is not already being processed, without waiting for it.</summary>
    /// <returns>How many feeds were started.</returns>
    public async Task<int> StartDueAsync(CancellationToken cancellationToken)
    {
        var feeds = await _feeds.ListAsync(cancellationToken);
        var now = _time.GetUtcNow();
        var due = new List<Feed>();

        lock (_lock)
        {
            var ids = feeds.Select(feed => feed.Id).ToHashSet();
            foreach (var stale in _schedules.Keys.Where(id => !ids.Contains(id)).ToList())
            {
                _schedules.Remove(stale);
            }

            _tasks.RemoveAll(task => task.IsCompleted);

            foreach (var feed in feeds)
            {
                if (_running.Contains(feed.Id))
                {
                    continue;
                }

                var schedule = _schedules.TryGetValue(feed.Id, out var known)
                    ? known with { Interval = feed.Settings.Interval }
                    : InitialSchedule(feed);
                _schedules[feed.Id] = schedule;

                if (DueAt(schedule) <= now)
                {
                    _running.Add(feed.Id);
                    due.Add(feed);
                }
            }
        }

        foreach (var feed in due)
        {
            var task = Task.Run(() => RunAsync(feed, cancellationToken), CancellationToken.None);
            lock (_lock)
            {
                _tasks.Add(task);
            }
        }

        return due.Count;
    }

    /// <summary>Completes when every feed started so far has finished processing.</summary>
    public Task WhenIdleAsync()
    {
        lock (_lock)
        {
            return Task.WhenAll(_tasks.ToArray());
        }
    }

    /// <summary>When the feed is due next, or <c>null</c> if it is unknown to the scheduler or being processed.</summary>
    public DateTimeOffset? NextDue(long feedId)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            if (_running.Contains(feedId) || !_schedules.TryGetValue(feedId, out var schedule))
            {
                return null;
            }

            var at = DueAt(schedule);
            return at < now ? now : at;
        }
    }

    public void Dispose() => _slots.Dispose();

    private static Schedule InitialSchedule(Feed feed)
    {
        var health = feed.Health;
        var last = (health.LastSuccessAt, health.LastErrorAt) switch
        {
            (null, var error) => error,
            (var success, null) => success,
            (var success, var error) => success > error ? success : error,
        };
        return new Schedule(last, feed.Settings.Interval, health.ConsecutiveFailures, null, null);
    }

    private DateTimeOffset DueAt(Schedule schedule) =>
        schedule.From is { } from ? from + Delay(schedule) : DateTimeOffset.MinValue;

    private TimeSpan Delay(Schedule schedule)
    {
        var delay = FetchBackoff.NextDelay(
            schedule.Interval, schedule.ConsecutiveFailures, _options.MaxBackoff, schedule.RetryAfter, _options.MaxRetryAfter);
        return schedule.DeliveryRetryAfter is { } retry && retry < delay ? retry : delay;
    }

    private async Task RunAsync(Feed feed, CancellationToken cancellationToken)
    {
        var schedule = new Schedule(null, feed.Settings.Interval, 0, null, null);
        var acquired = false;
        try
        {
            await _slots.WaitAsync(cancellationToken);
            acquired = true;

            await using var scope = _scopes.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<FeedProcessor>();
            var outcome = await processor.ProcessAsync(feed, cancellationToken);
            schedule = schedule with
            {
                ConsecutiveFailures = outcome.ConsecutiveFailures,
                RetryAfter = outcome.RetryAfter,
                DeliveryRetryAfter = outcome.DeliveryRetryAfter,
            };

            await NotifyObserversAsync(scope.ServiceProvider, feed, outcome, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.FeedCrashed(ex, feed.Id, feed.Settings.Name);
            schedule = schedule with { ConsecutiveFailures = feed.Health.ConsecutiveFailures + 1 };
        }
        finally
        {
            if (acquired)
            {
                _slots.Release();
            }

            schedule = schedule with { From = _time.GetUtcNow() };
            var delay = Delay(schedule);
            lock (_lock)
            {
                _running.Remove(feed.Id);
                _schedules[feed.Id] = schedule;
            }

            _logger.NextFetch(feed.Id, delay);
        }
    }

    private async Task NotifyObserversAsync(IServiceProvider services, Feed feed, FeedOutcome outcome, CancellationToken cancellationToken)
    {
        foreach (var observer in services.GetServices<IFeedCycleObserver>())
        {
            try
            {
                await observer.OnFeedProcessedAsync(feed, outcome, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.ObserverFailed(ex, feed.Id, feed.Settings.Name, observer.GetType().Name);
            }
        }
    }

    /// <summary>What the next fetch of a feed is computed from; the due time is derived with the current interval.</summary>
    /// <param name="From">When the last run ended, or <c>null</c> if the feed was never fetched (due immediately).</param>
    private readonly record struct Schedule(
        DateTimeOffset? From, TimeSpan Interval, int ConsecutiveFailures, TimeSpan? RetryAfter, TimeSpan? DeliveryRetryAfter);
}
