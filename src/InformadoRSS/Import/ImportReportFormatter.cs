using System.Globalization;
using System.Text;

namespace InformadoRSS.Import;

/// <summary>Plain text versions of the import and send-latest reports, for the command line.</summary>
public static class ImportReportFormatter
{
    public static string Format(ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var text = new StringBuilder();
        if (report.DryRun)
        {
            text.AppendLine("Dry run: nothing was written.");
        }

        if (report.TestChannelId is { } test)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Every destination redirected to test channel {test}{(report.TestGuildId is { } guild ? $" in guild {guild}" : string.Empty)}.");
        }

        foreach (var feed in report.Feeds)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"[{Label(feed.Action)}] {feed.Name}{Id(feed.FeedId)}");
            text.AppendLine(CultureInfo.InvariantCulture, $"    {feed.Url}");
            foreach (var note in feed.Notes)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"    - {note}");
            }

            foreach (var route in feed.Routes)
            {
                var state = route.Action == RouteImportAction.Skipped ? string.Empty : route.Enabled ? ", enabled" : ", disabled";
                var review = route.NeedsReview ? ", NEEDS REVIEW" : string.Empty;
                var channel = route.ChannelId is null ? string.Empty
                    : route.ChannelId == route.SourceChannelId ? $" -> {route.ChannelId}"
                    : $" -> {route.ChannelId} (was {route.SourceChannelId})";
                text.AppendLine(CultureInfo.InvariantCulture, $"    [{Label(route.Action)}] {route.Name}{Id(route.RouteId)}{channel}{state}{review}");
                foreach (var note in route.Notes)
                {
                    text.AppendLine(CultureInfo.InvariantCulture, $"        - {note}");
                }
            }
        }

        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Feeds: {report.FeedsCreated} created, {report.FeedsMerged} merged, {report.FeedsSkipped} skipped.");
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Routes: {report.RoutesCreated} created, {report.RoutesRetargeted} retargeted, {report.RoutesUnchanged} unchanged, "
            + $"{report.RoutesSkipped} skipped; {report.RoutesNeedingReview} imported disabled for review.");
        return text.ToString();
    }

    public static string Format(IReadOnlyList<SendLatestResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var text = new StringBuilder();
        foreach (var result in results)
        {
            var item = result.ItemTitle is null ? string.Empty : $": {result.ItemTitle}";
            text.AppendLine(CultureInfo.InvariantCulture,
                $"[{result.Status}] {result.FeedName} / {result.RouteName} (route {result.RouteId}){item}");
            foreach (var error in result.Errors)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"    - {error}");
            }
        }

        var sent = results.Count(result => result.Status == SendLatestStatus.Sent);
        text.AppendLine(CultureInfo.InvariantCulture, $"{sent} of {results.Count} routes sent their latest item.");
        return text.ToString();
    }

    private static string Label(FeedImportAction action) => action.ToString().ToLowerInvariant();

    private static string Label(RouteImportAction action) => action.ToString().ToLowerInvariant();

    private static string Id(long? id) => id is { } value ? $" (#{value})" : string.Empty;
}
