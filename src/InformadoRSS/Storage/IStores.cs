namespace InformadoRSS.Storage;

public interface IFeedStore
{
    Task<IReadOnlyList<Feed>> ListAsync(CancellationToken cancellationToken);

    Task<Feed?> GetAsync(long id, CancellationToken cancellationToken);

    /// <exception cref="DuplicateFeedException">Another feed already uses the same URL.</exception>
    Task<Feed> AddAsync(FeedSettings settings, CancellationToken cancellationToken);

    /// <returns><c>false</c> when the feed does not exist.</returns>
    /// <exception cref="DuplicateFeedException">Another feed already uses the same URL.</exception>
    Task<bool> UpdateAsync(long id, FeedSettings settings, CancellationToken cancellationToken);

    /// <summary>Deletes the feed together with its routes, destinations and seen items.</summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);

    /// <summary>Resets the failure count and stores the validators to send on the next fetch.</summary>
    Task RecordSuccessAsync(long id, DateTimeOffset at, FeedValidators validators, CancellationToken cancellationToken);

    /// <summary>Increments the failure count. Validators are kept.</summary>
    Task RecordFailureAsync(long id, DateTimeOffset at, string message, CancellationToken cancellationToken);
}

public interface IRouteStore
{
    Task<IReadOnlyList<Route>> ListByFeedAsync(long feedId, CancellationToken cancellationToken);

    Task<Route?> GetAsync(long id, CancellationToken cancellationToken);

    /// <returns><c>null</c> when the feed does not exist.</returns>
    Task<Route?> AddAsync(long feedId, RouteSettings settings, CancellationToken cancellationToken);

    /// <summary>Replaces the route settings, including the whole destination list. Seen items are kept.</summary>
    Task<bool> UpdateAsync(long id, RouteSettings settings, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
}

/// <summary>Per-route record of delivered (or deliberately skipped) item ids.</summary>
public interface ISeenItemStore
{
    /// <summary>Whether the route has seen any item yet, i.e. its first fetch already happened.</summary>
    Task<bool> HasAnyAsync(long routeId, CancellationToken cancellationToken);

    /// <summary>Returns the ids in <paramref name="itemIds"/> not yet seen by the route, preserving their order.</summary>
    Task<IReadOnlyList<string>> FilterUnseenAsync(long routeId, IReadOnlyList<string> itemIds, CancellationToken cancellationToken);

    /// <returns>How many ids were not seen before.</returns>
    Task<int> MarkSeenAsync(long routeId, IEnumerable<string> itemIds, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>Global key/value settings such as the Discord bot token.</summary>
public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Stores <paramref name="value"/>, or removes the key when it is <c>null</c>.</summary>
    Task SetAsync(string key, string? value, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>Thrown when a feed URL is already used by another feed.</summary>
public sealed class DuplicateFeedException(Uri url)
    : Exception($"A feed with URL '{url}' already exists.")
{
    public Uri Url { get; } = url;
}
