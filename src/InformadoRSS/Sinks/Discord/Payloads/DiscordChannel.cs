namespace InformadoRSS.Sinks.Discord;

/// <summary>A guild channel from <c>GET /guilds/{guild.id}/channels</c>.</summary>
/// <param name="Type">The channel type; see <see cref="DiscordChannelType"/>.</param>
/// <param name="ParentId">The category the channel belongs to, if any.</param>
public sealed record DiscordChannel(
    string Id,
    int Type,
    string? Name = null,
    int Position = 0,
    string? ParentId = null,
    IReadOnlyList<DiscordPermissionOverwrite>? PermissionOverwrites = null);
