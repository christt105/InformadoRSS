namespace InformadoRSS.Sinks.Discord;

/// <summary>Body of <c>POST /channels/{channel.id}/messages</c>.</summary>
public sealed record DiscordMessagePayload(
    string? Content,
    IReadOnlyList<DiscordEmbedPayload>? Embeds,
    DiscordAllowedMentions AllowedMentions);
