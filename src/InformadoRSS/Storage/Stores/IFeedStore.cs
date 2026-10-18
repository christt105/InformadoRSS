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
