namespace InformadoRSS.Storage;

public interface IRouteStore
{
    Task<IReadOnlyList<Route>> ListByFeedAsync(long feedId, CancellationToken cancellationToken);

    Task<Route?> GetAsync(long id, CancellationToken cancellationToken);

    /// <returns><c>null</c> when the feed does not exist.</returns>
    Task<Route?> AddAsync(long feedId, RouteSettings settings, CancellationToken cancellationToken);

    /// <summary>Replaces the route settings, including the whole destination list. Seen items and the seeded marker are kept.</summary>
    Task<bool> UpdateAsync(long id, RouteSettings settings, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Flags the destination at <paramref name="position"/> of the route as broken because a sink rejected it permanently,
    /// or clears the flag when <paramref name="reason"/> is <c>null</c>. Replacing the destinations of a route clears every flag.
    /// </summary>
    Task SetDestinationBrokenAsync(long routeId, int position, DateTimeOffset at, string? reason, CancellationToken cancellationToken);

    /// <summary>Every destination currently flagged as broken, ordered by route and position.</summary>
    Task<IReadOnlyList<BrokenDestination>> ListBrokenDestinationsAsync(CancellationToken cancellationToken);
}
