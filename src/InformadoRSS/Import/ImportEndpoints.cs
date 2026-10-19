using System.Text.Json;
using InformadoRSS.Sinks.Discord;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace InformadoRSS.Import;

public static class ImportEndpoints
{
    /// <summary>
    /// Maps <c>POST /api/import/monitorss</c>, whose body is a <c>mongoexport</c> dump of MonitoRSS's <c>userfeeds</c>
    /// collection (query: <c>testChannel</c>, <c>testGuild</c>, <c>dryRun</c>), and <c>POST /api/import/send-latest</c> (query:
    /// <c>channel</c>, <c>feedId</c>, <c>includeDisabled</c>). Both answer with their report.
    /// Requires <see cref="ImportServiceCollectionExtensions.AddMonitorssImport"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapImportApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/import");
        group.MapPost("/monitorss", ImportMonitorssAsync);
        group.MapPost("/send-latest", SendLatestAsync);
        return endpoints;
    }

    private static async Task<IResult> ImportMonitorssAsync(
        HttpRequest request,
        [FromQuery] string? testChannel,
        [FromQuery] string? testGuild,
        [FromQuery] bool? dryRun,
        [FromServices] MonitorssImporter importer,
        CancellationToken cancellationToken)
    {
        if (testChannel is not null && !SnowflakeComparer.IsSnowflake(testChannel))
        {
            return Error("invalid_id", "testChannel must be a numeric Discord id");
        }

        if (testGuild is not null && (testChannel is null || !SnowflakeComparer.IsSnowflake(testGuild)))
        {
            return Error("invalid_id", "testGuild must be a numeric Discord id and needs testChannel");
        }

        IReadOnlyList<MonitorssFeed> feeds;
        try
        {
            feeds = await MonitorssExportReader.ReadAsync(request.Body, cancellationToken);
        }
        catch (JsonException ex)
        {
            return Error("invalid_document", $"the body is not a MonitoRSS userfeeds export: {ex.Message}");
        }

        var report = await importer.ImportAsync(feeds, new ImportOptions(testChannel, dryRun == true, testGuild), cancellationToken);
        return TypedResults.Json(report, ImportJsonContext.Default.ImportReport);
    }

    private static async Task<IResult> SendLatestAsync(
        [FromQuery] string? channel,
        [FromQuery] long? feedId,
        [FromQuery] bool? includeDisabled,
        [FromServices] LatestItemSender sender,
        CancellationToken cancellationToken)
    {
        if (channel is not null && !SnowflakeComparer.IsSnowflake(channel))
        {
            return Error("invalid_id", "channel must be a numeric Discord id");
        }

        var results = await sender.SendAsync(new SendLatestOptions(channel, feedId, includeDisabled == true), cancellationToken);
        return TypedResults.Json(results, ImportJsonContext.Default.IReadOnlyListSendLatestResult);
    }

    private static JsonHttpResult<ImportEndpointError> Error(string code, string message) =>
        TypedResults.Json(new ImportEndpointError(code, message), ImportJsonContext.Default.ImportEndpointError, statusCode: 400);
}
