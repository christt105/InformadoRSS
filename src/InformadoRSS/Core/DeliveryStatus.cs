namespace InformadoRSS.Core;

/// <summary>Outcome category of a delivery attempt.</summary>
public enum DeliveryStatus
{
    Delivered,

    /// <summary>The attempt failed but may succeed later, e.g. a rate limit or a network error.</summary>
    Transient,

    /// <summary>The destination is unusable until someone fixes it, e.g. a deleted channel or missing permissions.</summary>
    Permanent,
}
