namespace InformadoRSS.Sinks.Discord;

/// <summary>A Discord user, limited to the fields InformadoRSS reads.</summary>
public sealed record DiscordUser(string Id, string? Username = null, bool? Bot = null);
