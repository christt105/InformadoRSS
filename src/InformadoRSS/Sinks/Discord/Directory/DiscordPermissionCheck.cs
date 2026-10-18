namespace InformadoRSS.Sinks.Discord;

/// <summary>One permission the bot needs in a channel.</summary>
/// <param name="Name">A stable identifier such as <c>sendMessages</c> or <c>createPosts</c>.</param>
/// <param name="Label">The name Discord shows in its permission settings.</param>
public sealed record DiscordPermissionCheck(string Name, string Label, bool Granted);
