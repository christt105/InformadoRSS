namespace InformadoRSS.Sinks.Discord;

/// <summary>An entry of <c>GET /users/@me/guilds</c>.</summary>
/// <param name="Icon">The icon hash, or <see langword="null"/> when the guild has no icon.</param>
public sealed record DiscordPartialGuild(string Id, string Name, string? Icon = null);
