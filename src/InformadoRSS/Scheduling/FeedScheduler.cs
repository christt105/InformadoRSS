using InformadoRSS.Storage;

namespace InformadoRSS.Scheduling;

/// <summary>
/// Decides which feeds are due and runs a <see cref="FeedProcessor"/> for each of them, never two at the same time for
/// the same feed and at most <see cref="SchedulerOptions.MaxConcurrency"/> overall. Due times live in memory; after a
/// restart they are rebuilt from the last success or failure stored with each feed.
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
    private readonly Dictionary<long, DateTimeOffset> _due = [];
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
            foreach (var stale in _due.Keys.Where(id => !ids.Contains(id)).ToList())
            {
                _due.Remove(stale);
            }

            _tasks.RemoveAll(task => task.IsCompleted);

            foreach (var feed in feeds)
            {
                if (_running.Contains(feed.Id))
                {
                    continue;
                }

                if (!_due.TryGetValue(feed.Id, out var at))
                {
                    at = InitialDue(feed, now);
                    _due[feed.Id] = at;
                }

                if (at <= now)
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
        lock (_lock)
        {
            return !_running.Contains(feedId) && _due.TryGetValue(feedId, out var at) ? at : null;
        }
    }

    public void Dispose() => _slots.Dispose();

    private DateTimeOffset InitialDue(Feed feed, DateTimeOffset now)
    {
        var health = feed.Health;
        var last = (health.LastSuccessAt, health.LastErrorAt) switch
        {
            (null, var error) => error,
            (var success, null) => success,
            (var success, var error) => success > error ? success : error,
        };
        if (last is null)
        {
            return now;
        }

        var at = last.Value + FetchBackoff.NextDelay(
            feed.Settings.Interval, health.ConsecutiveFailures, _options.MaxBackoff, null, _options.MaxRetryAfter);
        return at < now ? now : at;
    }

    private async Task RunAsync(Feed feed, CancellationToken cancellationToken)
    {
        var delay = feed.Settings.Interval;
        var acquired = false;
        try
        {
            await _slots.WaitAsync(cancellationToken);
            acquired = true;

            await using var scope = _scopes.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<FeedProcessor>();
            var outcome = await processor.ProcessAsync(feed, cancellationToken);
            delay = FetchBackoff.NextDelay(
                feed.Settings.Interval, outcome.ConsecutiveFailures, _options.MaxBackoff, outcome.RetryAfter, _options.MaxRetryAfter);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.FeedCrashed(ex, feed.Id, feed.Settings.Name);
            delay = FetchBackoff.NextDelay(
                feed.Settings.Interval, feed.Health.ConsecutiveFailures + 1, _options.MaxBackoff, null, _options.MaxRetryAfter);
        }
        finally
        {
            if (acquired)
            {
                _slots.Release();
            }

            lock (_lock)
            {
                _running.Remove(feed.Id);
                _due[feed.Id] = _time.GetUtcNow() + delay;
            }

            _logger.NextFetch(feed.Id, delay);
        }
    }
}
