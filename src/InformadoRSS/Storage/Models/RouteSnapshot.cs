using System.Text.Json;
using InformadoRSS.Core;

namespace InformadoRSS.Storage;

/// <summary>A route whose filter and template are embedded as JSON values rather than strings.</summary>
public sealed record RouteSnapshot(
    string Name,
    bool Enabled,
    IReadOnlyList<Destination> Destinations,
    JsonElement? Filter = null,
    JsonElement? Template = null);
