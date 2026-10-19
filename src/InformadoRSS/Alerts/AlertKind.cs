namespace InformadoRSS.Alerts;

public enum AlertKind
{
    /// <summary>A feed has gone without a successful fetch for longer than the configured threshold.</summary>
    FeedBroken,

    /// <summary>A feed that was alerted as broken was fetched successfully again.</summary>
    FeedRecovered,

    /// <summary>A sink rejected a route destination permanently.</summary>
    DestinationBroken,

    /// <summary>A destination that was alerted as broken delivered successfully again.</summary>
    DestinationRecovered,

    /// <summary>A scheduled database backup could not be written.</summary>
    BackupFailed,

    /// <summary>A backup succeeded after an alerted failure.</summary>
    BackupRecovered,
}
