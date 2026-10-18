using System.Text.Json;
using System.Text.Json.Serialization;
using InformadoRSS.Sinks.Discord;

namespace InformadoRSS;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(IReadOnlyList<DiscordGuildSummary>))]
[JsonSerializable(typeof(IReadOnlyList<DiscordChannelSummary>))]
[JsonSerializable(typeof(DiscordChannelPermissions))]
[JsonSerializable(typeof(DiscordTestMessageRequest))]
[JsonSerializable(typeof(DiscordTestMessageResponse))]
[JsonSerializable(typeof(DiscordEndpointError))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
