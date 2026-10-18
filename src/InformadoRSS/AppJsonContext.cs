using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
