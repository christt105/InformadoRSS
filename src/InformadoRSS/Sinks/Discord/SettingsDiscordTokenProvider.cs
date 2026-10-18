using InformadoRSS.Storage;

namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Reads the token from the <see cref="SettingsKey"/> setting, falling back to the <see cref="ConfigurationKey"/>
/// configuration value (environment variable <c>Discord__BotToken</c>).
/// </summary>
public sealed class SettingsDiscordTokenProvider(ISettingsStore settings, IConfiguration configuration) : IDiscordTokenProvider
{
    public const string SettingsKey = "discord.botToken";
    public const string ConfigurationKey = "Discord:BotToken";

    private const string BotPrefix = "Bot ";

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        var token = Normalize(await settings.GetAsync(SettingsKey, cancellationToken))
            ?? Normalize(configuration[ConfigurationKey]);
        return token;
    }

    private static string? Normalize(string? token)
    {
        token = token?.Trim();
        if (token is not null && token.StartsWith(BotPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[BotPrefix.Length..].Trim();
        }

        return string.IsNullOrEmpty(token) ? null : token;
    }
}
