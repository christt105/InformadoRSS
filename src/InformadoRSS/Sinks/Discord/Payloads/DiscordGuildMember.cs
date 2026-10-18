namespace InformadoRSS.Sinks.Discord;

/// <summary>A guild member from <c>GET /guilds/{guild.id}/members/{user.id}</c>.</summary>
/// <param name="Roles">Ids of the member's roles, without <c>@everyone</c>.</param>
public sealed record DiscordGuildMember(IReadOnlyList<string> Roles, DiscordUser? User = null);
