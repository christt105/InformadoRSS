namespace InformadoRSS.Storage;

/// <summary>
/// Per-route record of delivered (or deliberately skipped) item ids, plus which destinations already got an item that
/// is still pending for others.
/// </summary>
public interface ISeenItemStore
{
    /// <summary>Whether the route was seeded, i.e. its first fetch already happened, even if the feed was empty then.</summary>
    Task<bool> IsSeededAsync(long routeId, CancellationToken cancellationToken);

    /// <summary>Marks the route as seeded and <paramref name="itemIds"/> as seen, without sending them.</summary>
    /// <returns>How many ids were not seen before.</returns>
    Task<int> SeedAsync(long routeId, IEnumerable<string> itemIds, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Returns the ids in <paramref name="itemIds"/> not yet seen by the route, preserving their order.</summary>
    Task<IReadOnlyList<string>> FilterUnseenAsync(long routeId, IReadOnlyList<string> itemIds, CancellationToken cancellationToken);

    /// <summary>Marks the items seen and forgets their per-destination progress.</summary>
    /// <returns>How many ids were not seen before.</returns>
    Task<int> MarkSeenAsync(long routeId, IEnumerable<string> itemIds, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Positions of the route destinations already done with an item that is not seen yet.</summary>
    Task<IReadOnlySet<int>> ListDoneDestinationsAsync(long routeId, string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that the destination at <paramref name="position"/> is done with the item (delivered or skipped), so it is
    /// not sent again while other destinations still retry it. Replacing the destinations of a route forgets this progress.
    /// </summary>
    Task MarkDestinationDoneAsync(long routeId, int position, string itemId, DateTimeOffset at, CancellationToken cancellationToken);
}
