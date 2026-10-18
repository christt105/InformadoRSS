namespace InformadoRSS.Sinks.Discord;

/// <summary>A message as returned by <c>POST /channels/{channel.id}/messages</c>, limited to its ids.</summary>
public sealed record DiscordMessage(string Id, string? ChannelId = null);
