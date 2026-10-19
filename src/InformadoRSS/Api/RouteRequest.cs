using System.Text.Json;

namespace InformadoRSS.Api;

/// <summary>Body of <c>POST /api/feeds/{feedId}/routes</c>, <c>PUT /api/routes/{id}</c> and <c>POST /api/routes/validate</c>.</summary>
/// <param name="Enabled"><see langword="true"/> when absent.</param>
/// <param name="Filter">A filter tree as described by <c>FilterJson</c>; <see langword="null"/> lets every item through.</param>
/// <param name="Template">A message template as described by <c>TemplateJson</c>; <see langword="null"/> uses the default.</param>
public sealed record RouteRequest(
    string? Name,
    bool? Enabled = null,
    JsonElement? Filter = null,
    JsonElement? Template = null,
    IReadOnlyList<DestinationRequest?>? Destinations = null);
