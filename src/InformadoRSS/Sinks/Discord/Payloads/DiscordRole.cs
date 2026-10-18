namespace InformadoRSS.Sinks.Discord;

/// <param name="Permissions">The permission bit set as a decimal string.</param>
public sealed record DiscordRole(string Id, string Name, string Permissions, int Position = 0);
