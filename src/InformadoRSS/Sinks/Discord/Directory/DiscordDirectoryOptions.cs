namespace InformadoRSS.Sinks.Discord;

public sealed class DiscordDirectoryOptions
{
    /// <summary>How long guilds, channels, roles and the bot's member data are reused before asking Discord again.</summary>
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(1);
}
