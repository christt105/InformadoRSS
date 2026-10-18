namespace InformadoRSS.Sinks.Discord;

/// <summary>Which mentions in a message may notify someone. An empty <see cref="Parse"/> list disables them all.</summary>
public sealed record DiscordAllowedMentions(IReadOnlyList<string> Parse)
{
    public static DiscordAllowedMentions None { get; } = new([]);
}
