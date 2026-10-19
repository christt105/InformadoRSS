namespace InformadoRSS.Alerts;

internal static partial class AlertsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Alert: {Title}: {Message}")]
    public static partial void ProblemRaised(this ILogger logger, string title, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alert: {Title}: {Message}")]
    public static partial void RecoveryRaised(this ILogger logger, string title, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring invalid alert setting: {Problem}")]
    public static partial void InvalidSetting(this ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Information, Message = "No {Target} alert target configured; the alert was only logged")]
    public static partial void NoTarget(this ILogger logger, AlertTarget target);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Alert sent to {Target}")]
    public static partial void Sent(this ILogger logger, AlertTarget target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending the alert to {Target} failed temporarily: {Error}; retrying on the next cycle of the feed")]
    public static partial void Deferred(this ILogger logger, AlertTarget target, string? error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending the alert to {Target} failed: {Error}; the alert was only logged")]
    public static partial void Failed(this ILogger logger, AlertTarget target, string? error);
}
