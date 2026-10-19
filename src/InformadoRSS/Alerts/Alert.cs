namespace InformadoRSS.Alerts;

/// <summary>A notification about a feed or destination changing between working and broken.</summary>
/// <param name="Message">Plain text, possibly several lines.</param>
/// <param name="At">When the transition was detected.</param>
public sealed record Alert(AlertKind Kind, string Title, string Message, DateTimeOffset At)
{
    /// <summary>Whether the alert reports something broken rather than a recovery.</summary>
    public bool IsProblem => Kind is AlertKind.FeedBroken or AlertKind.DestinationBroken or AlertKind.BackupFailed;
}
