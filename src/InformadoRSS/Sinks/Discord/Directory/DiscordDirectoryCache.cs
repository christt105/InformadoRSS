using System.Diagnostics.CodeAnalysis;

namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Short-lived in-memory cache of Discord responses for <see cref="DiscordDirectory"/>. Entries expire after
/// <see cref="DiscordDirectoryOptions.CacheDuration"/>, and every entry is dropped when the bot token changes.
/// </summary>
public sealed class DiscordDirectoryCache(TimeProvider timeProvider, DiscordDirectoryOptions options)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (object Value, DateTimeOffset ExpiresAt)> _entries = new(StringComparer.Ordinal);
    private string? _token;

    public bool TryGet<T>(string token, string key, [NotNullWhen(true)] out T? value)
        where T : class
    {
        var now = timeProvider.GetUtcNow();
        lock (_lock)
        {
            SwitchToken(token);
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > now && entry.Value is T typed)
                {
                    value = typed;
                    return true;
                }

                _entries.Remove(key);
            }
        }

        value = null;
        return false;
    }

    public void Set<T>(string token, string key, T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        var now = timeProvider.GetUtcNow();
        lock (_lock)
        {
            SwitchToken(token);
            foreach (var expired in _entries.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToList())
            {
                _entries.Remove(expired);
            }

            _entries[key] = (value, now + options.CacheDuration);
        }
    }

    private void SwitchToken(string token)
    {
        if (!string.Equals(_token, token, StringComparison.Ordinal))
        {
            _entries.Clear();
            _token = token;
        }
    }
}
