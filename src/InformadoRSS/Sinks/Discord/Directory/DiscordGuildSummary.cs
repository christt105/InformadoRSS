namespace InformadoRSS.Sinks.Discord;

/// <summary>A guild the bot is in, as listed by <c>GET /api/discord/guilds</c>.</summary>
/// <param name="IconUrl">The guild icon on Discord's CDN, or <see langword="null"/> when it has none.</param>
public sealed record DiscordGuildSummary(string Id, string Name, string? IconUrl);
