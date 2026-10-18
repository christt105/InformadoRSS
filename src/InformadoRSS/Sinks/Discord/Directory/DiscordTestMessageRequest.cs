namespace InformadoRSS.Sinks.Discord;

/// <summary>Body of <c>POST /api/discord/channels/{channelId}/test-message</c>.</summary>
/// <param name="TemplateJson">A route template as stored (see <c>TemplateJson</c>); <see langword="null"/> for the default.</param>
public sealed record DiscordTestMessageRequest(string? TemplateJson = null);
