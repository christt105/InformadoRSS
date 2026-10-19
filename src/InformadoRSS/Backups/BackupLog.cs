namespace InformadoRSS.Backups;

internal static partial class BackupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Database backed up to {Path}; {Deleted} old backups deleted")]
    public static partial void Created(this ILogger logger, string path, int deleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Backing up the database to {Directory} failed; retrying on the next check")]
    public static partial void Failed(this ILogger logger, Exception exception, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring invalid backup setting: {Problem}")]
    public static partial void InvalidSetting(this ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Backup check failed unexpectedly")]
    public static partial void CheckCrashed(this ILogger logger, Exception exception);
}
