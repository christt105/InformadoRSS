namespace InformadoRSS.Backups;

public enum BackupResult
{
    /// <summary>No backup folder is configured.</summary>
    Disabled,

    /// <summary>The newest backup is more recent than the configured interval.</summary>
    NotDue,

    /// <summary>A new backup was written.</summary>
    Created,

    /// <summary>The backup could not be written; it is retried on the next check.</summary>
    Failed,
}
