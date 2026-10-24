namespace InformadoRSS.Sinks.Discord;

/// <summary>What is needed to compute the bot's permissions in the channels of a guild.</summary>
public sealed record DiscordBotMembership(DiscordGuild Guild, DiscordGuildMember Member, string UserId);
