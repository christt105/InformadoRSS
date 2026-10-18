namespace InformadoRSS.Storage;

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
