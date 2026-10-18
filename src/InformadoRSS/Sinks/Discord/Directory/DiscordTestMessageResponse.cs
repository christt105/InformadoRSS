namespace InformadoRSS.Sinks.Discord;

/// <summary>Answer to a test message that Discord accepted.</summary>
/// <param name="MessageId">The id of the posted message, when Discord returned it.</param>
public sealed record DiscordTestMessageResponse(string ChannelId, string? MessageId);
