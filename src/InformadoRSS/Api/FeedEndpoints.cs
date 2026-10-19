using System.Net;
using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using InformadoRSS.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace InformadoRSS.Api;

public static class FeedEndpoints
{
    /// <summary>How many items <c>POST /api/feeds/validate</c> returns.</summary>
    public const int PreviewItemCount = 5;

    private const string Example = "{\"url\":\"https://example.com/feed.xml\",\"name\":\"Example\",\"intervalSeconds\":600}";

    /// <summary>
    /// Maps the feed endpoints under <c>/api/feeds</c>: list (with health, newest item and routes), create, read,
    /// replace, delete, and <c>POST /api/feeds/validate</c> to fetch and parse a URL without saving it.
    /// </summary>
    public static IEndpointRouteBuilder MapFeedApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/feeds");
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/validate", ValidateAsync);
        group.MapGet("/{id:long}", GetAsync);
        group.MapPut("/{id:long}", UpdateAsync);
        group.MapDelete("/{id:long}", DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ILatestItemStore latestItems,
        [FromServices] FeedScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var latest = await latestItems.ListAsync(cancellationToken);
        var broken = await routes.ListBrokenDestinationsAsync(cancellationToken);
        var list = new List<FeedResponse>();
        foreach (var feed in await feeds.ListAsync(cancellationToken))
        {
            list.Add(await ToResponseAsync(feed, routes, latest, broken, scheduler, cancellationToken));
        }

        return TypedResults.Json<IReadOnlyList<FeedResponse>>(list, ApiJsonContext.Default.IReadOnlyListFeedResponse);
    }

    private static async Task<IResult> GetAsync(
        [FromRoute] long id,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ILatestItemStore latestItems,
        [FromServices] FeedScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var feed = await feeds.GetAsync(id, cancellationToken);
        return feed is null
            ? ApiResults.NotFound($"there is no feed {id}")
            : await JsonAsync(feed, routes, latestItems, scheduler, HttpStatusCode.OK, cancellationToken);
    }

    private static async Task<IResult> CreateAsync(
        HttpRequest request,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ILatestItemStore latestItems,
        [FromServices] FeedScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(request, ApiJsonContext.Default.FeedRequest, Example, cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var errors = FeedInput.Validate(body, out var settings);
        if (settings is null)
        {
            return InvalidFeed(errors);
        }

        try
        {
            var feed = await feeds.AddAsync(settings, cancellationToken);
            return await JsonAsync(feed, routes, latestItems, scheduler, HttpStatusCode.Created, cancellationToken);
        }
        catch (DuplicateFeedException ex)
        {
            return Duplicate(ex);
        }
    }

    private static async Task<IResult> UpdateAsync(
        [FromRoute] long id,
        HttpRequest request,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        [FromServices] ILatestItemStore latestItems,
        [FromServices] FeedScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(request, ApiJsonContext.Default.FeedRequest, Example, cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var errors = FeedInput.Validate(body, out var settings);
        if (settings is null)
        {
            return InvalidFeed(errors);
        }

        var previous = await feeds.GetAsync(id, cancellationToken);
        if (previous is null)
        {
            return ApiResults.NotFound($"there is no feed {id}");
        }

        try
        {
            if (!await feeds.UpdateAsync(id, settings, cancellationToken))
            {
                return ApiResults.NotFound($"there is no feed {id}");
            }
        }
        catch (DuplicateFeedException ex)
        {
            return Duplicate(ex);
        }

        if (previous.Settings.Url != settings.Url)
        {
            await latestItems.ClearAsync(id, cancellationToken);
        }

        var feed = await feeds.GetAsync(id, cancellationToken);
        return feed is null
            ? ApiResults.NotFound($"there is no feed {id}")
            : await JsonAsync(feed, routes, latestItems, scheduler, HttpStatusCode.OK, cancellationToken);
    }

    private static async Task<IResult> DeleteAsync(
        [FromRoute] long id,
        [FromServices] IFeedStore feeds,
        CancellationToken cancellationToken) =>
        await feeds.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : ApiResults.NotFound($"there is no feed {id}");

    private static async Task<IResult> ValidateAsync(
        HttpRequest request,
        [FromServices] ISource source,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(
            request, ApiJsonContext.Default.FeedValidationRequest, "{\"url\":\"https://example.com/feed.xml\"}", cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var urlError = FeedInput.ParseUrl(body.Url, out var url);
        if (url is null)
        {
            return InvalidFeed([new ApiFieldError("$.url", urlError ?? "the URL is required")]);
        }

        var result = await source.FetchAsync(new FeedDefinition(url), cancellationToken);
        if (result is not FeedFetched fetched)
        {
            var message = result is FeedFetchFailed failed ? failed.Error.Message : "the server did not return the feed";
            return ApiResults.Error(
                HttpStatusCode.UnprocessableEntity, new ApiError("fetch_failed", $"could not read the feed: {message}"));
        }

        var items = fetched.Items
            .Take(PreviewItemCount)
            .Select(item => new FeedItemPreview(
                item.Id, item.Title, item.Link, item.Author, item.Published, item.Categories, item.ImageUrl))
            .ToList();
        return TypedResults.Json(
            new FeedValidationResponse(url.AbsoluteUri, fetched.Title, fetched.Items.Count, items),
            ApiJsonContext.Default.FeedValidationResponse);
    }

    private static async Task<IResult> JsonAsync(
        Feed feed,
        IRouteStore routes,
        ILatestItemStore latestItems,
        FeedScheduler scheduler,
        HttpStatusCode status,
        CancellationToken cancellationToken)
    {
        var response = await ToResponseAsync(
            feed,
            routes,
            await latestItems.ListAsync(cancellationToken),
            await routes.ListBrokenDestinationsAsync(cancellationToken),
            scheduler,
            cancellationToken);
        return TypedResults.Json(response, ApiJsonContext.Default.FeedResponse, statusCode: (int)status);
    }

    private static async Task<FeedResponse> ToResponseAsync(
        Feed feed,
        IRouteStore routes,
        IReadOnlyDictionary<long, LatestItem> latest,
        IReadOnlyList<BrokenDestination> broken,
        FeedScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var feedRoutes = await routes.ListByFeedAsync(feed.Id, cancellationToken);
        return new FeedResponse(
            feed.Id,
            feed.Settings.Url.AbsoluteUri,
            feed.Settings.Name,
            (long)feed.Settings.Interval.TotalSeconds,
            feed.Health,
            latest.GetValueOrDefault(feed.Id),
            scheduler.NextDue(feed.Id),
            feedRoutes.Select(route => RouteEndpoints.ToResponse(route, broken)).ToList());
    }

    private static JsonHttpResult<ApiError> InvalidFeed(IReadOnlyList<ApiFieldError> errors) =>
        ApiResults.Error(HttpStatusCode.BadRequest, new ApiError("invalid_feed", "the feed is not valid", errors));

    private static JsonHttpResult<ApiError> Duplicate(DuplicateFeedException ex) =>
        ApiResults.Error(
            HttpStatusCode.Conflict,
            new ApiError("duplicate_feed", ex.Message, [new ApiFieldError("$.url", "another feed already uses this URL")]));
}
