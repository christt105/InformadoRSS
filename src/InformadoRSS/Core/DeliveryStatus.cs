namespace InformadoRSS.Core;

/// <summary>Outcome category of a delivery attempt.</summary>
public enum DeliveryStatus
{
    Delivered,

    /// <summary>The attempt failed but may succeed later, e.g. a rate limit or a network error.</summary>
    Transient,

    /// <summary>The destination is unusable until someone fixes it, e.g. a deleted channel or missing permissions.</summary>
    Permanent,

    /// <summary>
    /// The destination works but refused this particular message, e.g. a payload that is too long or otherwise invalid.
    /// Retrying the same message would fail again, so the item is skipped for that destination.
    /// </summary>
    Rejected,
}
