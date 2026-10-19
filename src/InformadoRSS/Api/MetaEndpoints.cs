using InformadoRSS.Core;
using InformadoRSS.Templates;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace InformadoRSS.Api;

public static class MetaEndpoints
{
    /// <summary>Maps <c>GET /api/meta</c>: version, registered sinks, template placeholders and feed interval limits.</summary>
    public static IEndpointRouteBuilder MapMetaApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/meta", GetMeta);
        return endpoints;
    }

    private static JsonHttpResult<MetaResponse> GetMeta([FromServices] IEnumerable<ISink> sinks) =>
        TypedResults.Json(
            new MetaResponse(
                AppInfo.Version,
                sinks.Select(sink => sink.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                TemplatePlaceholders.Supported,
                MessageTemplate.Default.Content ?? string.Empty,
                FeedInput.MinIntervalSeconds,
                FeedInput.DefaultIntervalSeconds,
                FeedInput.MaxIntervalSeconds),
            ApiJsonContext.Default.MetaResponse);
}
