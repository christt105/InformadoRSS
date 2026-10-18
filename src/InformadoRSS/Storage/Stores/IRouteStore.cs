namespace InformadoRSS.Storage;

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
