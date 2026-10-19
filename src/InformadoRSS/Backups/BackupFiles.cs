using System.Globalization;

namespace InformadoRSS.Backups;

/// <summary>
/// Names, lists and prunes the backup files in a folder. Files are named <c>informadorss-yyyyMMddTHHmmssZ.db</c> after
/// the UTC time they were taken, so their names sort chronologically and other files in the folder are left alone.
/// </summary>
public static class BackupFiles
{
    private const string Prefix = "informadorss-";
    private const string Extension = ".db";
    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

    public static string FileName(DateTimeOffset at) =>
        Prefix + at.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture) + Extension;

    /// <summary>The backups in <paramref name="directory"/> with the time each was taken, newest first.</summary>
    public static IReadOnlyList<(string Path, DateTimeOffset TakenAt)> List(string directory)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        return [.. System.IO.Directory.EnumerateFiles(directory, Prefix + "*" + Extension)
            .Select(path => (Path: path, TakenAt: ParseTimestamp(System.IO.Path.GetFileName(path))))
            .Where(file => file.TakenAt is not null)
            .Select(file => (file.Path, file.TakenAt!.Value))
            .OrderByDescending(file => file.Value)];
    }

    /// <summary>Deletes all but the newest <paramref name="keep"/> backups and returns how many were deleted.</summary>
    public static int Prune(string directory, int keep)
    {
        var stale = List(directory).Skip(keep).ToList();
        foreach (var (path, _) in stale)
        {
            File.Delete(path);
        }

        return stale.Count;
    }

    private static DateTimeOffset? ParseTimestamp(string fileName)
    {
        var timestamp = fileName[Prefix.Length..^Extension.Length];
        return DateTimeOffset.TryParseExact(
            timestamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : null;
    }
}
