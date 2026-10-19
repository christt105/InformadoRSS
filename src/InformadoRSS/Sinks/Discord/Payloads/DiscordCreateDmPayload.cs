namespace InformadoRSS.Sinks.Discord;

/// <summary>Body of <c>POST /users/@me/channels</c>.</summary>
public sealed record DiscordCreateDmPayload(string RecipientId);
