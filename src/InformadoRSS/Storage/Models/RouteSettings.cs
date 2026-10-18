using InformadoRSS.Core;

namespace InformadoRSS.Storage;

/// <summary>
/// User-editable part of a route. <paramref name="FilterJson"/> and <paramref name="TemplateJson"/> are opaque JSON
/// documents owned by the filter and template engines; <c>null</c> means "match everything" and "default template".
/// </summary>
public sealed record RouteSettings(
    string Name,
    string? FilterJson,
    string? TemplateJson,
    bool Enabled,
    IReadOnlyList<Destination> Destinations);
