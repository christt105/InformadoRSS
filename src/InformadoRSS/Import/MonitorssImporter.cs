using InformadoRSS.Core;
using InformadoRSS.Filters;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Storage;
using InformadoRSS.Templates;
using Route = InformadoRSS.Storage.Route;

namespace InformadoRSS.Import;

/// <summary>
/// Imports MonitoRSS feeds: each feed becomes a feed (feeds sharing a URL are merged, since URLs are unique here) and
/// each Discord channel connection becomes a route with one Discord destination.
/// </summary>
/// <remarks>
/// <para>
/// Created routes are left unseeded, so the first fetch of each feed marks every current item as seen without sending
/// it, exactly like a route added from the UI. Nothing is republished as long as MonitoRSS stops before that fetch.
/// </para>
/// <para>
/// Re-running is safe. Destinations remember the MonitoRSS connection they came from in
/// <see cref="ConnectionIdParameter"/>; a connection whose route already exists is left alone, except that its
/// destination moves to the requested channel, which is how a test import becomes the real one. Seen items are kept
/// when that happens. A connection without a remembered route is also skipped when the feed already has a route not
/// created by the importer that posts to its channel.
/// </para>
/// </remarks>
public sealed class MonitorssImporter(IFeedStore feeds, IRouteStore routes)
{
    /// <summary>Destination parameter holding the id of the MonitoRSS connection the destination was imported from.</summary>
    public const string ConnectionIdParameter = "monitorssConnectionId";

    public static TimeSpan DefaultInterval { get; } = TimeSpan.FromMinutes(10);

    /// <exception cref="ArgumentException"><see cref="ImportOptions.TestChannelId"/> is not a Discord id.</exception>
    public async Task<ImportReport> ImportAsync(
        IReadOnlyList<MonitorssFeed> source, ImportOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (options.TestChannelId is not null && !SnowflakeComparer.IsSnowflake(options.TestChannelId))
        {
            throw new ArgumentException($"Test channel id \"{options.TestChannelId}\" is not a Discord id.", nameof(options));
        }

        if (options.TestGuildId is not null && (options.TestChannelId is null || !SnowflakeComparer.IsSnowflake(options.TestGuildId)))
        {
            throw new ArgumentException("A test guild id must be a Discord id and needs a test channel id.", nameof(options));
        }

        var byUrl = new Dictionary<string, FeedState>(StringComparer.Ordinal);
        foreach (var feed in await feeds.ListAsync(cancellationToken))
        {
            byUrl[feed.Settings.Url.AbsoluteUri] = new FeedState(feed.Id, feed.Settings, loaded: false);
        }

        var reports = new List<FeedImportReport>();
        foreach (var feed in source)
        {
            reports.Add(await ImportFeedAsync(feed, byUrl, options, cancellationToken));
        }

        return new ImportReport(options.DryRun, options.TestChannelId, options.TestGuildId, reports);
    }

    private async Task<FeedImportReport> ImportFeedAsync(
        MonitorssFeed source, Dictionary<string, FeedState> byUrl, ImportOptions options, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return new FeedImportReport(
                source.Title,
                source.Url,
                FeedImportAction.Skipped,
                null,
                ["the URL is not an absolute http(s) URL"],
                [.. source.Connections.Select(connection => Skipped(connection, connection.ChannelId, "the feed was skipped"))]);
        }

        var notes = new List<string>(source.UnsupportedOptions);
        var feedDisabled = source.DisabledCode is not null;
        if (source.DisabledCode is { } code)
        {
            notes.Add($"disabled in MonitoRSS ({code}), so its routes are imported disabled");
        }

        var interval = source.RefreshRateSeconds is > 0 and var seconds ? TimeSpan.FromSeconds(seconds) : DefaultInterval;
        FeedImportAction action;
        if (byUrl.TryGetValue(url.AbsoluteUri, out var state))
        {
            action = FeedImportAction.Merged;
            if (!state.Loaded && state.Id is { } id)
            {
                state.Routes.AddRange(await routes.ListByFeedAsync(id, cancellationToken));
                state.Loaded = true;
            }

            if (state.Settings.Interval != interval)
            {
                notes.Add($"kept the existing feed \"{state.Settings.Name}\" every {state.Settings.Interval.TotalSeconds:0} s instead of {interval.TotalSeconds:0} s");
            }
        }
        else
        {
            action = FeedImportAction.Created;
            var settings = new FeedSettings(url, source.Title, interval);
            long? id = options.DryRun ? null : (await feeds.AddAsync(settings, cancellationToken)).Id;
            state = new FeedState(id, settings, loaded: true);
            byUrl[url.AbsoluteUri] = state;
        }

        var routeReports = new List<RouteImportReport>();
        foreach (var connection in source.Connections)
        {
            routeReports.Add(await ImportConnectionAsync(connection, state, feedDisabled, options, cancellationToken));
        }

        return new FeedImportReport(source.Title, url.AbsoluteUri, action, state.Id, notes, routeReports);
    }

    private async Task<RouteImportReport> ImportConnectionAsync(
        MonitorssConnection connection, FeedState state, bool feedDisabled, ImportOptions options, CancellationToken cancellationToken)
    {
        var sourceChannel = connection.ChannelId ?? connection.WebhookChannelId;
        if (!SnowflakeComparer.IsSnowflake(sourceChannel))
        {
            return Skipped(connection, sourceChannel, "the connection has no Discord channel id");
        }

        var target = options.TestChannelId ?? sourceChannel!;
        var destination = DiscordDestination(target, options.TestChannelId is null ? connection.GuildId : options.TestGuildId, connection.Id);

        if (connection.Id is not null
            && Find(state.Routes, d => d.Parameters.TryGetValue(ConnectionIdParameter, out var id) && id == connection.Id) is var (tagged, position))
        {
            var current = tagged.Settings.Destinations[position];
            current.Parameters.TryGetValue(DiscordSink.ChannelIdParameter, out var channel);
            current.Parameters.TryGetValue(DiscordSink.GuildIdParameter, out var guild);
            destination.Parameters.TryGetValue(DiscordSink.GuildIdParameter, out var targetGuild);
            if (channel == target && guild == targetGuild)
            {
                return Existing(connection, tagged, sourceChannel, target, $"already imported as route {tagged.Id}");
            }

            var destinations = tagged.Settings.Destinations.ToList();
            destinations[position] = destination;
            var updated = tagged with { Settings = tagged.Settings with { Destinations = destinations } };
            if (!options.DryRun)
            {
                await routes.UpdateAsync(tagged.Id, updated.Settings, cancellationToken);
            }

            state.Routes[state.Routes.IndexOf(tagged)] = updated;
            return new RouteImportReport(
                connection.Name,
                RouteImportAction.Retargeted,
                tagged.Id,
                sourceChannel,
                target,
                tagged.Settings.Enabled,
                false,
                [$"route {tagged.Id} moved from channel {channel} to {target}; its seen items are kept"]);
        }

        if (Find(state.Routes, d => !d.Parameters.ContainsKey(ConnectionIdParameter)
                && d.Parameters.TryGetValue(DiscordSink.ChannelIdParameter, out var id) && id == sourceChannel) is var (sameChannel, _))
        {
            return Existing(connection, sameChannel, sourceChannel, target, $"route {sameChannel.Id} of this feed already posts to channel {sourceChannel}");
        }

        var (settings, needsReview, notes) = Translate(connection, feedDisabled, destination);
        long? routeId = null;
        if (!options.DryRun)
        {
            var created = await routes.AddAsync(state.Id!.Value, settings, cancellationToken)
                ?? throw new InvalidOperationException($"Feed {state.Id} disappeared during the import.");
            routeId = created.Id;
        }

        state.Routes.Add(new Route(routeId ?? 0, state.Id ?? 0, settings));
        return new RouteImportReport(
            connection.Name, RouteImportAction.Created, routeId, sourceChannel, target, settings.Enabled, needsReview, notes);
    }

    private static (RouteSettings Settings, bool NeedsReview, List<string> Notes) Translate(
        MonitorssConnection connection, bool feedDisabled, Destination destination)
    {
        var notes = new List<string>();
        var needsReview = false;

        if (connection.ChannelId is null)
        {
            notes.Add("posted through a webhook; the bot now posts in the webhook's channel, without its name and avatar");
            needsReview = true;
        }

        if (connection.ChannelType is { } type)
        {
            notes.Add($"channel type \"{type}\" is not supported; items are sent as plain messages to the channel");
            needsReview = true;
        }

        if (connection.HasThreadOptions)
        {
            notes.Add("thread and forum post options were not translated");
            needsReview = true;
        }

        if (connection.EmbedCount > 0)
        {
            notes.Add($"{connection.EmbedCount} embed(s) were not translated; only the text content was imported");
            needsReview = true;
        }

        if (connection.HasComponents)
        {
            notes.Add("buttons and other message components were not translated");
            needsReview = true;
        }

        var template = MonitorssTemplateTranslator.Translate(connection.Content);
        notes.AddRange(template.Notes);
        if (template.Template is null && connection.EmbedCount > 0)
        {
            notes.Add("the text content was empty, so the default template is used");
        }

        var filter = MonitorssFilterTranslator.Translate(connection.FilterExpression);
        notes.AddRange(filter.Notes);
        if (!filter.IsTranslated)
        {
            notes.AddRange(filter.Problems.Select(problem => $"filter not translated: {problem}"));
            notes.Add("imported without a filter");
            needsReview = true;
        }

        notes.AddRange(connection.IgnoredOptions.Select(option => $"ignored {option}"));

        if (connection.DisabledCode is { } code)
        {
            notes.Add($"disabled in MonitoRSS ({code})");
        }

        if (needsReview)
        {
            notes.Add("imported disabled: review the route, then enable it");
        }

        var enabled = !needsReview && !feedDisabled && connection.DisabledCode is null;
        var settings = new RouteSettings(
            connection.Name,
            filter.Root is null ? null : FilterJson.Serialize(filter.Root),
            template.Template is null ? null : TemplateJson.Serialize(template.Template),
            enabled,
            [destination]);
        return (settings, needsReview, notes);
    }

    private static Destination DiscordDestination(string channelId, string? guildId, string? connectionId)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { [DiscordSink.ChannelIdParameter] = channelId };
        if (guildId is not null)
        {
            parameters[DiscordSink.GuildIdParameter] = guildId;
        }

        if (connectionId is not null)
        {
            parameters[ConnectionIdParameter] = connectionId;
        }

        return new Destination(DiscordSink.SinkName, parameters);
    }

    private static (Route Route, int Position)? Find(List<Route> candidates, Func<Destination, bool> predicate)
    {
        foreach (var route in candidates)
        {
            var destinations = route.Settings.Destinations;
            for (var position = 0; position < destinations.Count; position++)
            {
                if (destinations[position].Sink == DiscordSink.SinkName && predicate(destinations[position]))
                {
                    return (route, position);
                }
            }
        }

        return null;
    }

    private static RouteImportReport Existing(MonitorssConnection connection, Route route, string? sourceChannel, string target, string note) =>
        new(
            connection.Name,
            RouteImportAction.Unchanged,
            route.Id == 0 ? null : route.Id,
            sourceChannel,
            target,
            route.Settings.Enabled,
            false,
            [note]);

    private static RouteImportReport Skipped(MonitorssConnection connection, string? sourceChannel, string note) =>
        new(connection.Name, RouteImportAction.Skipped, null, sourceChannel, null, false, false, [note]);

    private sealed class FeedState(long? id, FeedSettings settings, bool loaded)
    {
        public long? Id { get; } = id;

        public FeedSettings Settings { get; } = settings;

        public bool Loaded { get; set; } = loaded;

        public List<Route> Routes { get; } = [];
    }
}
