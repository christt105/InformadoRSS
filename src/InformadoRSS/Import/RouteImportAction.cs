using System.Text.Json.Serialization;

namespace InformadoRSS.Import;

/// <summary>What the importer did with a MonitoRSS connection.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RouteImportAction>))]
public enum RouteImportAction
{
    /// <summary>A new route was created.</summary>
    [JsonStringEnumMemberName("created")]
    Created,

    /// <summary>The route imported earlier from this connection now points to a different channel.</summary>
    [JsonStringEnumMemberName("retargeted")]
    Retargeted,

    /// <summary>A route for this connection or channel already exists and was left as it is.</summary>
    [JsonStringEnumMemberName("unchanged")]
    Unchanged,

    /// <summary>The connection could not be imported at all.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
