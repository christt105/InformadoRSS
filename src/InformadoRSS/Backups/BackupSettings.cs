using System.Globalization;
using InformadoRSS.Storage;

namespace InformadoRSS.Backups;

/// <summary>
/// Backup configuration, read from the settings store. Backups are off until <see cref="Directory"/> is set. Invalid
/// numbers fall back to their defaults and are reported in <see cref="Problems"/>.
/// </summary>
public sealed record BackupSettings
{
    /// <summary>Folder, as seen from inside the container, that receives the backup files.</summary>
    public const string DirectoryKey = "backups.directory";

    /// <summary>Whole hours between two backups.</summary>
    public const string IntervalHoursKey = "backups.intervalHours";

    /// <summary>How many backup files to keep; older ones are deleted after each new backup.</summary>
    public const string KeepKey = "backups.keep";

    public static TimeSpan DefaultInterval { get; } = TimeSpan.FromHours(24);

    public const int DefaultKeep = 14;

    public string? Directory { get; init; }

    public TimeSpan Interval { get; init; } = DefaultInterval;

    public int Keep { get; init; } = DefaultKeep;

    /// <summary>Descriptions of the invalid values that were ignored.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    public static async Task<BackupSettings> LoadAsync(ISettingsStore settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return FromValues(await settings.ListAsync(cancellationToken));
    }

    public static BackupSettings FromValues(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var problems = new List<string>();
        var hours = ReadPositive(values, IntervalHoursKey, (int)DefaultInterval.TotalHours, "a positive number of hours", problems);
        var keep = ReadPositive(values, KeepKey, DefaultKeep, "a positive number of files", problems);
        return new BackupSettings
        {
            Directory = Read(values, DirectoryKey),
            Interval = TimeSpan.FromHours(hours),
            Keep = keep,
            Problems = problems,
        };
    }

    private static int ReadPositive(
        IReadOnlyDictionary<string, string> values, string key, int fallback, string expected, List<string> problems)
    {
        if (Read(values, key) is not { } raw)
        {
            return fallback;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return value;
        }

        problems.Add($"{key} must be {expected}, not \"{raw}\"");
        return fallback;
    }

    private static string? Read(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
