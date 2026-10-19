namespace InformadoRSS.Storage;

/// <summary>Alerting state of a feed that is currently failing.</summary>
/// <param name="FailingSince">When the first failure after the last success was observed.</param>
/// <param name="AlertedAt">When the "feed broken" alert was sent, or <c>null</c> if it was not sent yet.</param>
public sealed record FeedAlertState(DateTimeOffset FailingSince, DateTimeOffset? AlertedAt);
