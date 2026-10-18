using System.Text.Json;
using System.Text.Json.Serialization;
using InformadoRSS.Core;

namespace InformadoRSS.Storage;

/// <summary>Portable copy of the whole configuration: feeds, routes, destinations and settings, without fetch state.</summary>
public sealed record ConfigurationSnapshot(
    int Version,
    IReadOnlyList<FeedSnapshot> Feeds,
    IReadOnlyDictionary<string, string> Settings)
{
    public const int CurrentVersion = 1;
}

public sealed record FeedSnapshot(Uri Url, string Name, long IntervalSeconds, IReadOnlyList<RouteSnapshot> Routes);

/// <summary>A route whose filter and template are embedded as JSON values rather than strings.</summary>
public sealed record RouteSnapshot(
    string Name,
    bool Enabled,
    IReadOnlyList<Destination> Destinations,
    JsonElement? Filter = null,
    JsonElement? Template = null);

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConfigurationSnapshot))]
internal sealed partial class StorageJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ColumnJsonContext : JsonSerializerContext;
