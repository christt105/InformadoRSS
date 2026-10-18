namespace InformadoRSS.Sinks.Discord;

/// <summary>A Discord embed object, limited to the fields InformadoRSS renders.</summary>
public sealed record DiscordEmbedPayload(
    string? Title = null,
    string? Description = null,
    string? Url = null,
    int? Color = null,
    DateTimeOffset? Timestamp = null,
    DiscordEmbedMedia? Image = null,
    DiscordEmbedMedia? Thumbnail = null,
    DiscordEmbedFooter? Footer = null,
    DiscordEmbedAuthor? Author = null);
