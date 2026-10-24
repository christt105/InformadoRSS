using System.Globalization;

namespace InformadoRSS.Scheduling;

/// <summary>Settings of the feed scheduler, read from the <c>Scheduler</c> configuration section.</summary>
public sealed record SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>Whether the background scheduler runs at all.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How many feeds may be fetched and delivered at the same time.</summary>
    public int MaxConcurrency { get; init; } = 4;

    /// <summary>How many items a route may deliver per cycle; the rest wait for the next cycle.</summary>
    public int MaxItemsPerRoute { get; init; } = 5;

    /// <summary>Items published longer ago than this are marked seen without being sent, e.g. after the service was down for a while.</summary>
    public TimeSpan MaxItemAge { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Upper bound of the failure backoff, unless the feed interval itself is longer.</summary>
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Upper bound applied to a <c>Retry-After</c> requested by a server.</summary>
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromHours(24);

    /// <summary>How often the scheduler looks for due feeds.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <exception cref="InvalidOperationException">A value is present but invalid.</exception>
    public static SchedulerOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var defaults = new SchedulerOptions();
        return new SchedulerOptions
        {
            Enabled = ReadBoolean(section, nameof(Enabled), defaults.Enabled),
            MaxConcurrency = ReadPositive(section, nameof(MaxConcurrency), defaults.MaxConcurrency),
            MaxItemsPerRoute = ReadPositive(section, nameof(MaxItemsPerRoute), defaults.MaxItemsPerRoute),
            MaxItemAge = ReadDuration(section, nameof(MaxItemAge), defaults.MaxItemAge),
            MaxBackoff = ReadDuration(section, nameof(MaxBackoff), defaults.MaxBackoff),
            MaxRetryAfter = ReadDuration(section, nameof(MaxRetryAfter), defaults.MaxRetryAfter),
            PollInterval = ReadDuration(section, nameof(PollInterval), defaults.PollInterval),
        };
    }

    private static bool ReadBoolean(IConfigurationSection section, string key, bool fallback)
    {
        if (section[key] is not { } raw || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return bool.TryParse(raw.Trim(), out var value) ? value : throw Invalid(key, raw);
    }

    private static int ReadPositive(IConfigurationSection section, string key, int fallback)
    {
        if (section[key] is not { } raw || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw Invalid(key, raw);
    }

    private static TimeSpan ReadDuration(IConfigurationSection section, string key, TimeSpan fallback)
    {
        if (section[key] is not { } raw || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return TimeSpan.TryParse(raw.Trim(), CultureInfo.InvariantCulture, out var value) && value > TimeSpan.Zero
            ? value
            : throw Invalid(key, raw);
    }

    private static InvalidOperationException Invalid(string key, string raw) =>
        new($"Invalid value '{raw}' for {SectionName}:{key}; expected a positive value.");
}
