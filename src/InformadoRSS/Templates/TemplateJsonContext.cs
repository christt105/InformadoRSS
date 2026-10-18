using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Templates;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MessageTemplate))]
internal sealed partial class TemplateJsonContext : JsonSerializerContext;
