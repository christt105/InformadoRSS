namespace InformadoRSS.Sinks.Discord;

/// <summary>A channel messages can be posted to, as listed by <c>GET /api/discord/guilds/{guildId}/channels</c>.</summary>
/// <param name="Type"><c>text</c> or <c>announcement</c>.</param>
/// <param name="CategoryId">The category the channel is in, if any.</param>
public sealed record DiscordChannelSummary(string Id, string Name, string Type, string? CategoryId, string? CategoryName, int Position);
