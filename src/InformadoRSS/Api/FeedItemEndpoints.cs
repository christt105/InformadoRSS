using System.Net;
using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sources;
using InformadoRSS.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace InformadoRSS.Api;

public static class FeedItemEndpoints
{
    /// <summary>
    /// Maps the manual item tools of a feed under <c>/api/feeds/{id}/items</c>: list the items the feed has right now with
    /// what each route did with them, <c>POST /reset</c> to make routes forget an item so the next cycle treats it as new,
    /// and <c>POST /send</c> to send one item now, through a route template, without touching any state.
    /// </summary>
    public static IEndpointRouteBuilder MapFeedItemApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/feeds/{id:long}/items");
        group.MapGet("", ListAsync);
        group.MapPost("/reset", ResetAsync);
        group.MapPost("/send", SendAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        [FromRoute] long id,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ISeenItemStore seenItems,
        [FromServices] ISource source,
        [FromServices] SchedulerOptions options,
        [FromServices] TimeProvider time,
        CancellationToken cancellationToken)
    {
        var feed = await feeds.GetAsync(id, cancellationToken);
        if (feed is null)
        {
            return ApiResults.NotFound($"there is no feed {id}");
        }

        var (items, failure) = await FetchAsync(source, feed, cancellationToken);
        if (items is null)
        {
            return failure!;
        }

        var feedRoutes = await routes.ListByFeedAsync(id, cancellationToken);
        var ids = items.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var unseenByRoute = new Dictionary<long, HashSet<string>>();
        foreach (var route in feedRoutes)
        {
            unseenByRoute[route.Id] = (await seenItems.FilterUnseenAsync(route.Id, ids, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        }

        var oldest = time.GetUtcNow() - options.MaxItemAge;
        var statuses = FeedProcessor.OldestFirst(items).AsEnumerable().Reverse()
            .Select(item => new FeedItemStatus(
                item.Id,
                item.Title,
                item.Link,
                item.Published,
                item.Published < oldest,
                [.. feedRoutes.Select(route => new RouteItemState(route.Id, route.Settings.Name, !unseenByRoute[route.Id].Contains(item.Id)))]))
            .ToList();
        return TypedResults.Json(
            new FeedItemsResponse((long)options.MaxItemAge.TotalSeconds, statuses), ApiJsonContext.Default.FeedItemsResponse);
    }

    private static async Task<IResult> ResetAsync(
        [FromRoute] long id,
        HttpRequest request,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ISeenItemStore seenItems,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(
            request, ApiJsonContext.Default.ItemResetRequest, "{\"itemId\":\"abc\",\"routeId\":1}", cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        if (string.IsNullOrEmpty(body.ItemId))
        {
            return Invalid("$.itemId", "the item id is required");
        }

        if (await feeds.GetAsync(id, cancellationToken) is null)
        {
            return ApiResults.NotFound($"there is no feed {id}");
        }

        var targets = (await routes.ListByFeedAsync(id, cancellationToken))
            .Where(route => body.RouteId is null || route.Id == body.RouteId)
            .ToList();
        if (body.RouteId is not null && targets.Count == 0)
        {
            return ApiResults.NotFound($"feed {id} has no route {body.RouteId}");
        }

        var reset = 0;
        foreach (var route in targets)
        {
            if (await seenItems.ForgetAsync(route.Id, body.ItemId, cancellationToken))
            {
                reset++;
            }
        }

        return TypedResults.Json(new ItemResetResponse(reset), ApiJsonContext.Default.ItemResetResponse);
    }

    private static async Task<IResult> SendAsync(
        [FromRoute] long id,
        HttpRequest request,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ISource source,
        [FromServices] IMessageRenderer renderer,
        [FromServices] IEnumerable<ISink> sinks,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(
            request,
            ApiJsonContext.Default.ItemSendRequest,
            "{\"itemId\":\"abc\",\"routeId\":1,\"channelId\":\"123456789012345678\"}",
            cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        if (string.IsNullOrEmpty(body.ItemId))
        {
            return Invalid("$.itemId", "the item id is required");
        }

        if (body.RouteId is null)
        {
            return Invalid("$.routeId", "the route whose template renders the message is required");
        }

        if (body.ChannelId is not null && !SnowflakeComparer.IsSnowflake(body.ChannelId))
        {
            return Invalid("$.channelId", "the channel must be a numeric Discord id");
        }

        var feed = await feeds.GetAsync(id, cancellationToken);
        if (feed is null)
        {
            return ApiResults.NotFound($"there is no feed {id}");
        }

        var route = await routes.GetAsync(body.RouteId.Value, cancellationToken);
        if (route is null || route.FeedId != id)
        {
            return ApiResults.NotFound($"feed {id} has no route {body.RouteId}");
        }

        var (items, failure) = await FetchAsync(source, feed, cancellationToken);
        if (items is null)
        {
            return failure!;
        }

        var item = items.FirstOrDefault(candidate => candidate.Id == body.ItemId);
        if (item is null)
        {
            return ApiResults.NotFound("the feed no longer has that item");
        }

        RenderedMessage message;
        try
        {
            message = renderer.Render(route.Settings.TemplateJson, item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiResults.Error(HttpStatusCode.UnprocessableEntity, new ApiError("render_failed", $"the route template failed: {ex.Message}"));
        }

        IReadOnlyList<Destination> destinations = body.ChannelId is { } channelId
            ? [new Destination(DiscordSink.SinkName, new Dictionary<string, string> { [DiscordSink.ChannelIdParameter] = channelId })]
            : route.Settings.Destinations;

        var available = new Dictionary<string, ISink>(StringComparer.Ordinal);
        foreach (var candidate in sinks)
        {
            available.TryAdd(candidate.Name, candidate);
        }

        var errors = new List<string>();
        foreach (var destination in destinations)
        {
            if (!available.TryGetValue(destination.Sink, out var sink))
            {
                errors.Add($"{destination.Sink}: no such sink");
                continue;
            }

            var result = await sink.DeliverAsync(destination, message, cancellationToken);
            if (result.Status != DeliveryStatus.Delivered)
            {
                errors.Add($"{destination.Sink}: {result.Status}: {result.Error}");
            }
        }

        return TypedResults.Json(new ItemSendResponse(errors.Count == 0, errors), ApiJsonContext.Default.ItemSendResponse);
    }

    private static async Task<(IReadOnlyList<FeedItem>? Items, IResult? Failure)> FetchAsync(
        ISource source, Feed feed, CancellationToken cancellationToken)
    {
        var result = await source.FetchAsync(new FeedDefinition(feed.Settings.Url), cancellationToken);
        if (result is not FeedFetched fetched)
        {
            var message = result is FeedFetchFailed failed ? failed.Error.Message : "the server did not return the feed";
            return (null, ApiResults.Error(
                HttpStatusCode.UnprocessableEntity, new ApiError("fetch_failed", $"could not read the feed: {message}")));
        }

        return (ItemIdentity.Apply(fetched.Items, feed.Settings.IdentityPattern), null);
    }

    private static JsonHttpResult<ApiError> Invalid(string path, string message) =>
        ApiResults.Error(HttpStatusCode.BadRequest, new ApiError("invalid_request", message, [new ApiFieldError(path, message)]));
}
