using System.Net;

namespace InformadoRSS.Scheduling;

internal static partial class SchedulerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Feed scheduler disabled through Scheduler:Enabled")]
    public static partial void Disabled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Feed scheduler started: up to {MaxConcurrency} feeds at a time, {MaxItemsPerRoute} items per route per cycle")]
    public static partial void Started(this ILogger logger, int maxConcurrency, int maxItemsPerRoute);

    [LoggerMessage(Level = LogLevel.Error, Message = "Feed scheduler cycle failed")]
    public static partial void CycleFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing feed {FeedId} ({FeedName}) failed unexpectedly")]
    public static partial void FeedCrashed(this ILogger logger, Exception exception, long feedId, string feedName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Feed {FeedId} ({FeedName}): cycle observer {Observer} failed")]
    public static partial void ObserverFailed(this ILogger logger, Exception exception, long feedId, string feedName, string observer);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetching feed {FeedId} ({FeedName}) failed ({Kind} {StatusCode}, {ConsecutiveFailures} in a row): {Error}")]
    public static partial void FetchFailed(
        this ILogger logger, long feedId, string feedName, Core.FetchErrorKind kind, HttpStatusCode? statusCode, int consecutiveFailures, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next fetch of feed {FeedId} in {Delay}")]
    public static partial void NextFetch(this ILogger logger, long feedId, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Information, Message = "Route {RouteId} ({RouteName}) seeded with {Count} existing items without sending them")]
    public static partial void RouteSeeded(this ILogger logger, long routeId, string routeName, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Route {RouteId} ({RouteName}) skipped: invalid filter: {Errors}")]
    public static partial void InvalidFilter(this ILogger logger, long routeId, string routeName, string errors);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {RouteId} ({RouteName}): filter regex timed out on item {ItemId}; item skipped")]
    public static partial void FilterTimeout(this ILogger logger, long routeId, string routeName, string itemId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Route {RouteId} ({RouteName}): rendering item {ItemId} failed; retrying next cycle")]
    public static partial void RenderFailed(this ILogger logger, Exception exception, long routeId, string routeName, string itemId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {RouteId} ({RouteName}): no sink named '{Sink}' is registered; retrying next cycle")]
    public static partial void UnknownSink(this ILogger logger, long routeId, string routeName, string sink);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {RouteId} ({RouteName}): temporary failure delivering item {ItemId} to {Sink} destination #{Position}: {Error}; retrying later")]
    public static partial void DeliveryTransient(
        this ILogger logger, long routeId, string routeName, string itemId, string sink, int position, string? error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Route {RouteId} ({RouteName}): item {ItemId} held back for {Sink} destination #{Position}, which asked to wait {Wait}")]
    public static partial void DeliveryDeferred(
        this ILogger logger, long routeId, string routeName, string itemId, string sink, int position, TimeSpan wait);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Route {RouteId} ({RouteName}): {Sink} destination #{Position} rejected item {ItemId}: {Error}; item skipped for that destination")]
    public static partial void DeliveryRejected(
        this ILogger logger, long routeId, string routeName, string itemId, string sink, int position, string? error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Route {RouteId} ({RouteName}): {Sink} destination #{Position} is broken and rejected item {ItemId}: {Error}")]
    public static partial void DeliveryPermanent(
        this ILogger logger, long routeId, string routeName, string itemId, string sink, int position, string? error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Route {RouteId} ({RouteName}) delivered {Count} items; {Pending} left for the next cycle")]
    public static partial void RouteDelivered(this ILogger logger, long routeId, string routeName, int count, int pending);
}
