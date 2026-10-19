using InformadoRSS.Core;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Sinks.Discord;

public static class DiscordSinkServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DiscordSink"/> as an <see cref="ISink"/>, the typed <see cref="DiscordRestClient"/>, a shared
    /// <see cref="DiscordRateLimiter"/>, the <see cref="SettingsDiscordTokenProvider"/> and the definition of its setting.
    /// Requires the storage services.
    /// </summary>
    public static IServiceCollection AddDiscordSink(this IServiceCollection services, DiscordClientOptions? options = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(options ?? new DiscordClientOptions());
        services.TryAddSingleton<DiscordRateLimiter>();
        services.TryAddSingleton<IDiscordTokenProvider, SettingsDiscordTokenProvider>();
        services.AddHttpClient<DiscordRestClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<ISink, DiscordSink>();
        services.AddSettingDefinition(new SettingDefinition(
            SettingsDiscordTokenProvider.SettingsKey,
            "Bot token",
            "Token of the Discord bot that posts the messages, from the Developer Portal. "
                + "When empty, the Discord__BotToken environment variable is used.",
            Secret: true,
            Group: "Discord"));
        return services;
    }
}
