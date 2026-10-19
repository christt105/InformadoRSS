using System.Globalization;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;

namespace InformadoRSS.Alerts;

/// <summary>Builds the text of every alert.</summary>
public static class AlertMessages
{
    private const int MaxTitleLength = 200;
    private const int MaxReasonLength = 500;

    public static Alert FeedBroken(Feed feed, FeedAlertState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(state);

        var name = feed.Settings.Name;
        var failures = feed.Health.ConsecutiveFailures;
        var message =
            $"{name} has been failing for {FormatDuration(now - state.FailingSince)} " +
            $"({failures.ToString(CultureInfo.InvariantCulture)} {(failures == 1 ? "failure" : "failures")} in a row).\n" +
            $"URL: {feed.Settings.Url.AbsoluteUri}\n" +
            $"Last error: {Truncate(feed.Health.LastError ?? "unknown", MaxReasonLength)}";
        return new Alert(AlertKind.FeedBroken, Title($"Feed failing: {name}"), message, now);
    }

    public static Alert FeedRecovered(Feed feed, DateTimeOffset failingSince, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var name = feed.Settings.Name;
        var message =
            $"{name} is fetched successfully again after failing for {FormatDuration(now - failingSince)}.\n" +
            $"URL: {feed.Settings.Url.AbsoluteUri}";
        return new Alert(AlertKind.FeedRecovered, Title($"Feed recovered: {name}"), message, now);
    }

    public static Alert DestinationBroken(Feed feed, DestinationAlertState destination, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(destination);

        var since = destination.BrokenSince ?? now;
        var message =
            $"Route {destination.RouteName} of feed {feed.Settings.Name} cannot deliver to {Describe(destination)} " +
            $"since {since.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC; " +
            "new items are skipped for it until it works again.\n" +
            $"Reason: {Truncate(destination.BrokenReason ?? "unknown", MaxReasonLength)}";
        return new Alert(AlertKind.DestinationBroken, Title($"Destination broken: {destination.RouteName}"), message, now);
    }

    public static Alert DestinationRecovered(Feed feed, DestinationAlertState destination, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(destination);

        var message = $"Route {destination.RouteName} of feed {feed.Settings.Name} delivers to {Describe(destination)} again.";
        return new Alert(AlertKind.DestinationRecovered, Title($"Destination working again: {destination.RouteName}"), message, now);
    }

    /// <summary>A coarse human duration such as <c>6h 5m</c> or <c>2d 3h</c>.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
        {
            return "less than a minute";
        }

        var days = (int)duration.TotalDays;
        return days > 0
            ? duration.Hours > 0 ? $"{days}d {duration.Hours}h" : $"{days}d"
            : duration.Hours > 0
                ? duration.Minutes > 0 ? $"{duration.Hours}h {duration.Minutes}m" : $"{duration.Hours}h"
                : $"{duration.Minutes}m";
    }

    private static string Describe(DestinationAlertState destination)
    {
        var parameters = destination.Destination.Parameters;
        var target = destination.Destination.Sink switch
        {
            DiscordSink.SinkName when parameters.TryGetValue(DiscordSink.ChannelIdParameter, out var channelId) =>
                $"Discord channel {channelId}",
            NtfySink.SinkName when parameters.TryGetValue(NtfySink.TopicParameter, out var topic) => $"ntfy topic {topic}",
            var sink => $"{sink} destination",
        };
        return $"{target} (destination {(destination.Position + 1).ToString(CultureInfo.InvariantCulture)})";
    }

    private static string Title(string title) => Truncate(title, MaxTitleLength);

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "...";
}
