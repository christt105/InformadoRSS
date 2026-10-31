using System.Globalization;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;

namespace InformadoRSS.Alerts;

/// <summary>
/// Alert configuration, read from the settings store. With the Discord target, alerts go to
/// <see cref="DiscordChannelId"/> if set, otherwise as a direct message to <see cref="DiscordUserId"/>; with the ntfy
/// target, to <see cref="NtfyTopic"/> on the server of <see cref="NtfySettings"/>. Without a usable target alerts are
/// only logged. Invalid values fall back to their defaults and are reported in <see cref="Problems"/>.
/// </summary>
public sealed record AlertSettings
{
    /// <summary><c>discord</c> (default) or <c>ntfy</c>.</summary>
    public const string TargetKey = "alerts.target";

    public const string DiscordChannelIdKey = "alerts.discordChannelId";

    public const string DiscordUserIdKey = "alerts.discordUserId";

    public const string NtfyTopicKey = "alerts.ntfyTopic";

    /// <summary>Whole minutes a feed may go without a successful fetch before it is reported as broken.</summary>
    public const string FeedBrokenAfterMinutesKey = "alerts.feedBrokenAfterMinutes";

    public static TimeSpan DefaultFeedBrokenAfter { get; } = TimeSpan.FromHours(24);

    public AlertTarget Target { get; init; } = AlertTarget.Discord;

    public string? DiscordChannelId { get; init; }

    public string? DiscordUserId { get; init; }

    public string? NtfyTopic { get; init; }

    /// <summary>
    /// How long a feed may keep failing before the "feed broken" alert, measured from the first failure after its last
    /// success. Time based rather than a failure count, so daily outages of several hours (such as YouTube feeds answering
    /// 404 for up to about 9 hours every night) do not raise alerts.
    /// </summary>
    public TimeSpan FeedBrokenAfter { get; init; } = DefaultFeedBrokenAfter;

    /// <summary>Descriptions of the invalid values that were ignored.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    public static async Task<AlertSettings> LoadAsync(ISettingsStore settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return FromValues(await settings.ListAsync(cancellationToken));
    }

    public static AlertSettings FromValues(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var problems = new List<string>();

        var target = AlertTarget.Discord;
        if (Read(values, TargetKey) is { } rawTarget)
        {
            if (rawTarget.Equals("discord", StringComparison.OrdinalIgnoreCase))
            {
                target = AlertTarget.Discord;
            }
            else if (rawTarget.Equals("ntfy", StringComparison.OrdinalIgnoreCase))
            {
                target = AlertTarget.Ntfy;
            }
            else
            {
                problems.Add($"{TargetKey} must be \"discord\" or \"ntfy\", not \"{rawTarget}\"");
            }
        }

        var brokenAfter = DefaultFeedBrokenAfter;
        if (Read(values, FeedBrokenAfterMinutesKey) is { } rawMinutes)
        {
            if (int.TryParse(rawMinutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0)
            {
                brokenAfter = TimeSpan.FromMinutes(minutes);
            }
            else
            {
                problems.Add($"{FeedBrokenAfterMinutesKey} must be a positive number of minutes, not \"{rawMinutes}\"");
            }
        }

        return new AlertSettings
        {
            Target = target,
            DiscordChannelId = ReadSnowflake(values, DiscordChannelIdKey, problems),
            DiscordUserId = ReadSnowflake(values, DiscordUserIdKey, problems),
            NtfyTopic = ReadTopic(values, problems),
            FeedBrokenAfter = brokenAfter,
            Problems = problems,
        };
    }

    private static string? Read(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string? ReadSnowflake(IReadOnlyDictionary<string, string> values, string key, List<string> problems)
    {
        if (Read(values, key) is not { } raw)
        {
            return null;
        }

        if (raw.Length <= 20 && raw.All(char.IsAsciiDigit))
        {
            return raw;
        }

        problems.Add($"{key} must be a numeric Discord id, not \"{raw}\"");
        return null;
    }

    private static string? ReadTopic(IReadOnlyDictionary<string, string> values, List<string> problems)
    {
        if (Read(values, NtfyTopicKey) is not { } raw)
        {
            return null;
        }

        if (NtfySettings.IsValidTopic(raw))
        {
            return raw;
        }

        problems.Add($"{NtfyTopicKey} must be a valid ntfy topic, not \"{raw}\"");
        return null;
    }
}
