namespace InformadoRSS.Alerts;

/// <summary>What happened to an alert handed to <see cref="AlertSender"/>.</summary>
public enum AlertDispatch
{
    /// <summary>Delivered to the configured target.</summary>
    Sent,

    /// <summary>No usable target is configured, so the alert was only logged.</summary>
    Logged,

    /// <summary>The target refused the alert in a way retrying would not fix; it was logged instead.</summary>
    Failed,

    /// <summary>The target is temporarily unavailable; the alert should be sent again later.</summary>
    Deferred,
}
