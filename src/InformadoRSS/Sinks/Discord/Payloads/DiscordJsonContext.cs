using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Sinks.Discord;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiscordMessagePayload))]
[JsonSerializable(typeof(DiscordErrorBody))]
[JsonSerializable(typeof(DiscordUser))]
[JsonSerializable(typeof(List<DiscordPartialGuild>))]
[JsonSerializable(typeof(DiscordGuild))]
[JsonSerializable(typeof(List<DiscordChannel>))]
[JsonSerializable(typeof(DiscordGuildMember))]
[JsonSerializable(typeof(DiscordMessage))]
internal sealed partial class DiscordJsonContext : JsonSerializerContext;
