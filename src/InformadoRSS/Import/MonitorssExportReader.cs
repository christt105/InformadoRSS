using System.Globalization;
using System.Text.Json;

namespace InformadoRSS.Import;

/// <summary>
/// Reads a dump of MonitoRSS's <c>userfeeds</c> Mongo collection as written by <c>mongoexport</c>: either a JSON array
/// (<c>--jsonArray</c>) or one document per line, in relaxed or canonical Extended JSON.
/// </summary>
public static class MonitorssExportReader
{
    private static readonly string[] FormatterFlags = ["formatTables", "stripImages", "disableImageLinkPreviews", "ignoreNewLines"];

    /// <exception cref="JsonException">The input is not JSON or a document lacks <c>title</c> or <c>url</c>.</exception>
    public static IReadOnlyList<MonitorssFeed> Read(ReadOnlySpan<byte> utf8Json)
    {
        var feeds = new List<MonitorssFeed>();
        var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions { AllowMultipleValues = true, CommentHandling = JsonCommentHandling.Skip });
        while (reader.Read())
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    feeds.Add(ReadFeed(element, feeds.Count));
                }
            }
            else
            {
                feeds.Add(ReadFeed(root, feeds.Count));
            }
        }

        return feeds;
    }

    /// <inheritdoc cref="Read(ReadOnlySpan{byte})"/>
    public static async Task<IReadOnlyList<MonitorssFeed>> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    private static MonitorssFeed ReadFeed(JsonElement feed, int index)
    {
        if (feed.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Document {index} is not an object.");
        }

        var title = String(feed, "title") ?? throw new JsonException($"Document {index} has no \"title\".");
        var url = String(feed, "url") ?? throw new JsonException($"Document {index} ({title}) has no \"url\".");

        var unsupported = new List<string>();
        if (Property(feed, "dateCheckOptions") is { } dateCheck && Number(dateCheck, "oldArticleDateDiffMsThreshold") is > 0 and var threshold)
        {
            unsupported.Add($"skips articles published more than {Duration(threshold)} ago (dateCheckOptions)");
        }

        if (Count(feed, "passingComparisons") + Count(feed, "blockingComparisons") > 0)
        {
            unsupported.Add("passing/blocking comparisons (re-sending items whose fields changed)");
        }

        if (Property(feed, "formatOptions") is { ValueKind: JsonValueKind.Object } format
            && format.EnumerateObject().Any(option => option.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)))
        {
            unsupported.Add("custom date format, timezone or locale (formatOptions)");
        }

        if (Count(feed, "externalProperties") > 0)
        {
            unsupported.Add("external properties scraped from the article page");
        }

        if (String(feed, "scheduleMode") is { } scheduleMode)
        {
            unsupported.Add($"schedule mode \"{scheduleMode}\"");
        }

        var connections = new List<MonitorssConnection>();
        if (Property(feed, "connections") is { } wrapper && Property(wrapper, "discordChannels") is { ValueKind: JsonValueKind.Array } channels)
        {
            foreach (var connection in channels.EnumerateArray())
            {
                connections.Add(ReadConnection(connection));
            }
        }

        return new MonitorssFeed(
            Id(feed, "_id"),
            title,
            url,
            Number(feed, "userRefreshRateSeconds") ?? Number(feed, "refreshRateSeconds"),
            String(feed, "disabledCode"),
            unsupported,
            connections);
    }

    private static MonitorssConnection ReadConnection(JsonElement connection)
    {
        var details = Property(connection, "details");
        var channel = details is { } d ? Property(d, "channel") : null;
        var webhook = details is { } w ? Property(w, "webhook") : null;

        var ignored = new List<string>();
        if (details is { } detailsValue)
        {
            if (Count(detailsValue, "placeholderLimits") > 0)
            {
                ignored.Add("placeholder character limits");
            }

            if (Property(detailsValue, "formatter") is { ValueKind: JsonValueKind.Object } formatter)
            {
                ignored.AddRange(FormatterFlags
                    .Where(flag => Property(formatter, flag) is { ValueKind: JsonValueKind.True })
                    .Select(flag => $"formatter option {flag}"));
            }

            if (Property(detailsValue, "enablePlaceholderFallback") is { ValueKind: JsonValueKind.True })
            {
                ignored.Add("placeholder fallbacks");
            }
        }

        if (Count(connection, "customPlaceholders") > 0)
        {
            ignored.Add("custom placeholders");
        }

        if (Property(connection, "mentions") is { } mentions && Count(mentions, "targets") is > 0 and var mentionCount)
        {
            ignored.Add($"{mentionCount} mention target(s)");
        }

        if (Count(connection, "rateLimits") > 0)
        {
            ignored.Add("custom rate limits");
        }

        if (Property(connection, "splitOptions") is { } split && Property(split, "isEnabled") is { ValueKind: JsonValueKind.True })
        {
            ignored.Add("message splitting");
        }

        var hasThreadOptions = details is { } t
            && (String(t, "forumThreadTitle") is not null
                || String(t, "channelNewThreadTitle") is not null
                || Count(t, "forumThreadTags") > 0
                || (webhook is { } wh && String(wh, "threadId") is not null));

        JsonElement? expression = Property(connection, "filters") is { } filters
            && Property(filters, "expression") is { ValueKind: JsonValueKind.Object } value
            ? value.Clone()
            : null;

        return new MonitorssConnection(
            Id(connection, "id") ?? Id(connection, "_id"),
            String(connection, "name") ?? "Connection",
            String(connection, "disabledCode"),
            channel is { } c ? String(c, "id") : null,
            channel is { } g ? String(g, "guildId") : webhook is { } wg ? String(wg, "guildId") : null,
            channel is { } ct ? String(ct, "type") : null,
            webhook is { } wc ? String(wc, "channelId") : null,
            details is { } content ? String(content, "content") : null,
            expression,
            details is { } e ? Count(e, "embeds") : 0,
            details is { } r && (Count(r, "componentRows") > 0
                || Property(r, "componentsV2") is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) }),
            hasThreadOptions,
            ignored);
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value
            : null;

    private static string? String(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value && value.GetString() is { Length: > 0 } text ? text : null;

    private static int Count(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : 0;

    /// <summary>Reads an id written as <c>{"$oid": "..."}</c> or as a plain string.</summary>
    private static string? Id(JsonElement element, string name) => Property(element, name) switch
    {
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        { ValueKind: JsonValueKind.Object } value => String(value, "$oid"),
        _ => null,
    };

    /// <summary>Reads a number written plainly or as canonical <c>{"$numberInt"|"$numberLong"|"$numberDouble": "..."}</c>.</summary>
    private static long? Number(JsonElement element, string name)
    {
        switch (Property(element, name))
        {
            case { ValueKind: JsonValueKind.Number } value:
                return value.TryGetInt64(out var integer) ? integer : (long)value.GetDouble();
            case { ValueKind: JsonValueKind.Object } value:
                var text = String(value, "$numberInt") ?? String(value, "$numberLong") ?? String(value, "$numberDouble");
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? (long)parsed : null;
            default:
                return null;
        }
    }

    private static string Duration(long milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);
        return span.TotalDays >= 1 && span.TotalDays == Math.Floor(span.TotalDays)
            ? $"{span.TotalDays.ToString(CultureInfo.InvariantCulture)} days"
            : span.ToString("c", CultureInfo.InvariantCulture);
    }
}
