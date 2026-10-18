namespace InformadoRSS.Sinks.Discord;

/// <summary>A guild from <c>GET /guilds/{guild.id}</c>, limited to what permission checks need.</summary>
/// <param name="Roles">Every role of the guild, including <c>@everyone</c>, whose id is the guild id.</param>
public sealed record DiscordGuild(string Id, string Name, string OwnerId, IReadOnlyList<DiscordRole> Roles);
