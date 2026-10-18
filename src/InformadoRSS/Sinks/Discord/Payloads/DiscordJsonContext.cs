using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Sinks.Discord;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiscordMessagePayload))]
[JsonSerializable(typeof(DiscordErrorBody))]
internal sealed partial class DiscordJsonContext : JsonSerializerContext;
