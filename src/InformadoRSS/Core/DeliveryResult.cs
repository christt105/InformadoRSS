namespace InformadoRSS.Core;

/// <summary>Result of <see cref="ISink.DeliverAsync"/>. Expected failures are returned, not thrown.</summary>
public sealed record DeliveryResult(DeliveryStatus Status, string? Error = null, TimeSpan? RetryAfter = null)
{
    public static DeliveryResult Delivered { get; } = new(DeliveryStatus.Delivered);

    public static DeliveryResult Transient(string error, TimeSpan? retryAfter = null) =>
        new(DeliveryStatus.Transient, error, retryAfter);

    public static DeliveryResult Permanent(string error) => new(DeliveryStatus.Permanent, error);

    public static DeliveryResult Rejected(string error) => new(DeliveryStatus.Rejected, error);
}
