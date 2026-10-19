using System.Net;
using System.Text.Json;
using InformadoRSS.Core;
using InformadoRSS.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Route = InformadoRSS.Storage.Route;

namespace InformadoRSS.Api;

public static class RouteEndpoints
{
    private const string Example =
        "{\"name\":\"All items\",\"enabled\":true,\"filter\":null,\"template\":null,"
        + "\"destinations\":[{\"sink\":\"discord\",\"parameters\":{\"guildId\":\"1\",\"channelId\":\"2\"}}]}";

    /// <summary>
    /// Maps the route endpoints: list and create under <c>/api/feeds/{feedId}/routes</c>, read, replace and delete under
    /// <c>/api/routes/{id}</c>, and <c>POST /api/routes/validate</c> to check a route without saving it.
    /// </summary>
    public static IEndpointRouteBuilder MapRouteApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/feeds/{feedId:long}/routes", ListAsync);
        endpoints.MapPost("/api/feeds/{feedId:long}/routes", CreateAsync);
        endpoints.MapPost("/api/routes/validate", ValidateAsync);
        endpoints.MapGet("/api/routes/{id:long}", GetAsync);
        endpoints.MapPut("/api/routes/{id:long}", UpdateAsync);
        endpoints.MapDelete("/api/routes/{id:long}", DeleteAsync);
        return endpoints;
    }

    internal static RouteResponse ToResponse(Route route, IReadOnlyList<BrokenDestination> broken)
    {
        var destinations = route.Settings.Destinations
            .Select((destination, position) =>
            {
                var flag = broken.FirstOrDefault(b => b.RouteId == route.Id && b.Position == position);
                return new DestinationResponse(destination.Sink, destination.Parameters, flag?.Since, flag?.Reason);
            })
            .ToList();
        return new RouteResponse(
            route.Id,
            route.FeedId,
            route.Settings.Name,
            route.Settings.Enabled,
            ParseJson(route.Settings.FilterJson),
            ParseJson(route.Settings.TemplateJson),
            destinations);
    }

    private static async Task<IResult> ListAsync(
        [FromRoute] long feedId,
        [FromServices] IFeedStore feeds,
        [FromServices] IRouteStore routes,
        CancellationToken cancellationToken)
    {
        if (await feeds.GetAsync(feedId, cancellationToken) is null)
        {
            return ApiResults.NotFound($"there is no feed {feedId}");
        }

        var broken = await routes.ListBrokenDestinationsAsync(cancellationToken);
        var list = (await routes.ListByFeedAsync(feedId, cancellationToken)).Select(route => ToResponse(route, broken)).ToList();
        return TypedResults.Json<IReadOnlyList<RouteResponse>>(list, ApiJsonContext.Default.IReadOnlyListRouteResponse);
    }

    private static async Task<IResult> CreateAsync(
        [FromRoute] long feedId,
        HttpRequest request,
        [FromServices] IRouteStore routes,
        [FromServices] IEnumerable<ISink> sinks,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(request, ApiJsonContext.Default.RouteRequest, Example, cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var validation = RouteInput.Validate(body, SinkNames(sinks), out var settings);
        if (settings is null)
        {
            return Invalid(validation);
        }

        var route = await routes.AddAsync(feedId, settings, cancellationToken);
        return route is null
            ? ApiResults.NotFound($"there is no feed {feedId}")
            : TypedResults.Json(ToResponse(route, []), ApiJsonContext.Default.RouteResponse, statusCode: (int)HttpStatusCode.Created);
    }

    private static async Task<IResult> ValidateAsync(
        HttpRequest request,
        [FromServices] IEnumerable<ISink> sinks,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(request, ApiJsonContext.Default.RouteRequest, Example, cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var validation = RouteInput.Validate(body, SinkNames(sinks), out _);
        return TypedResults.Json(validation, ApiJsonContext.Default.RouteValidationResponse);
    }

    private static async Task<IResult> GetAsync(
        [FromRoute] long id,
        [FromServices] IRouteStore routes,
        CancellationToken cancellationToken)
    {
        var route = await routes.GetAsync(id, cancellationToken);
        return route is null
            ? ApiResults.NotFound($"there is no route {id}")
            : TypedResults.Json(
                ToResponse(route, await routes.ListBrokenDestinationsAsync(cancellationToken)), ApiJsonContext.Default.RouteResponse);
    }

    private static async Task<IResult> UpdateAsync(
        [FromRoute] long id,
        HttpRequest request,
        [FromServices] IRouteStore routes,
        [FromServices] IEnumerable<ISink> sinks,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(request, ApiJsonContext.Default.RouteRequest, Example, cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var validation = RouteInput.Validate(body, SinkNames(sinks), out var settings);
        if (settings is null)
        {
            return Invalid(validation);
        }

        if (!await routes.UpdateAsync(id, settings, cancellationToken))
        {
            return ApiResults.NotFound($"there is no route {id}");
        }

        var route = await routes.GetAsync(id, cancellationToken);
        return route is null
            ? ApiResults.NotFound($"there is no route {id}")
            : TypedResults.Json(
                ToResponse(route, await routes.ListBrokenDestinationsAsync(cancellationToken)), ApiJsonContext.Default.RouteResponse);
    }

    private static async Task<IResult> DeleteAsync(
        [FromRoute] long id,
        [FromServices] IRouteStore routes,
        CancellationToken cancellationToken) =>
        await routes.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : ApiResults.NotFound($"there is no route {id}");

    private static JsonHttpResult<ApiError> Invalid(RouteValidationResponse validation) =>
        ApiResults.Error(
            HttpStatusCode.BadRequest,
            new ApiError("invalid_route", "the route is not valid", validation.Errors, validation.FilterErrors, validation.TemplateErrors));

    private static HashSet<string> SinkNames(IEnumerable<ISink> sinks) => sinks.Select(sink => sink.Name).ToHashSet(StringComparer.Ordinal);

    private static JsonElement? ParseJson(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Null ? null : document.RootElement.Clone();
    }
}
