namespace InformadoRSS.Sinks.Discord;

/// <summary>A channel messages can be posted to, as listed by <c>GET /api/discord/guilds/{guildId}/channels</c>.</summary>
/// <param name="Type"><c>text</c> or <c>announcement</c>.</param>
/// <param name="CategoryId">The category the channel is in, if any.</param>
/// <param name="CanPost">
/// Whether the bot has every permission it needs in the channel, or <c>null</c> when its permissions in the guild
/// could not be read.
/// </param>
public sealed record DiscordChannelSummary(
    string Id,
    string Name,
    string Type,
    string? CategoryId,
    string? CategoryName,
    int Position,
    bool? CanPost);
