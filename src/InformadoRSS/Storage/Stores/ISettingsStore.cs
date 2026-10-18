namespace InformadoRSS.Storage;

/// <summary>Global key/value settings such as the Discord bot token.</summary>
public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Stores <paramref name="value"/>, or removes the key when it is <c>null</c>.</summary>
    Task SetAsync(string key, string? value, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> ListAsync(CancellationToken cancellationToken);
}
