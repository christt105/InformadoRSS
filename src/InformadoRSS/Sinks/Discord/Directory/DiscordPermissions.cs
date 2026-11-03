using System.Globalization;

namespace InformadoRSS.Sinks.Discord;

/// <summary>Discord permission bits used by InformadoRSS, and parsing of Discord's decimal permission strings.</summary>
public static class DiscordPermissions
{
    public const ulong None = 0;
    public const ulong All = ulong.MaxValue;

    public const ulong Administrator = 1UL << 3;
    public const ulong ViewChannel = 1UL << 10;
    public const ulong SendMessages = 1UL << 11;
    public const ulong SendTtsMessages = 1UL << 12;
    public const ulong EmbedLinks = 1UL << 14;
    public const ulong AttachFiles = 1UL << 15;
    public const ulong ReadMessageHistory = 1UL << 16;
    public const ulong MentionEveryone = 1UL << 17;

    /// <summary>Permissions that are implicitly lost without <see cref="SendMessages"/>.</summary>
    public const ulong DependOnSendMessages = SendTtsMessages | EmbedLinks | AttachFiles | MentionEveryone;

    /// <summary>Parses a decimal permission string; anything unreadable counts as no permissions.</summary>
    public static ulong Parse(string? value) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var permissions) ? permissions : None;
}
