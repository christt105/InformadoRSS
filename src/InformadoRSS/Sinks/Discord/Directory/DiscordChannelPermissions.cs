namespace InformadoRSS.Sinks.Discord;

/// <summary>The bot's effective permissions in a channel, for warnings before a destination is saved.</summary>
/// <param name="ChannelType">The <see cref="DiscordChannelSummary.Type"/> of the channel.</param>
/// <param name="CanPost">Whether every permission in <paramref name="Permissions"/> is granted.</param>
/// <param name="Permissions">The permissions InformadoRSS needs for this channel type.</param>
public sealed record DiscordChannelPermissions(
    string GuildId,
    string ChannelId,
    string ChannelType,
    bool CanPost,
    IReadOnlyList<DiscordPermissionCheck> Permissions);
