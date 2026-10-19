namespace InformadoRSS.Api;

public static class ApplicationApi
{
    /// <summary>
    /// Maps the management API used by the web UI: <see cref="FeedEndpoints"/>, <see cref="RouteEndpoints"/>,
    /// <see cref="SettingsEndpoints"/> and <see cref="MetaEndpoints"/>. Requires the storage, source and scheduler services.
    /// </summary>
    public static IEndpointRouteBuilder MapApplicationApi(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapFeedApi().MapRouteApi().MapSettingsApi().MapMetaApi();
}
