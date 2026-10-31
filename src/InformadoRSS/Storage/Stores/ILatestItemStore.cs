namespace InformadoRSS.Storage;

/// <summary>Remembers the newest item of each feed. Rows are removed together with their feed.</summary>
public interface ILatestItemStore
{
    /// <summary>
    /// Stores <paramref name="item"/> as the newest item of the feed, replacing the previous one. Storing the same item
    /// again writes nothing, so <see cref="LatestItem.FetchedAt"/> keeps the first fetch that returned it.
    /// </summary>
    Task SetAsync(long feedId, LatestItem item, CancellationToken cancellationToken);

    /// <summary>Forgets the newest item of the feed, e.g. after its URL changed.</summary>
    Task ClearAsync(long feedId, CancellationToken cancellationToken);

    /// <summary>The newest item of every feed that has one, keyed by feed id.</summary>
    Task<IReadOnlyDictionary<long, LatestItem>> ListAsync(CancellationToken cancellationToken);
}
