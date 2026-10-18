using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Filters;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    AllowOutOfOrderMetadataProperties = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(FilterNode))]
internal sealed partial class FilterJsonContext : JsonSerializerContext;
