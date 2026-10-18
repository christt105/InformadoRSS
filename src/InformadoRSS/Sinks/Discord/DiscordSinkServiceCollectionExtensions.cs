using InformadoRSS.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Sinks.Discord;

public static class DiscordSinkServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DiscordSink"/> as an <see cref="ISink"/>, the typed <see cref="DiscordRestClient"/>, a shared
    /// <see cref="DiscordRateLimiter"/> and the <see cref="SettingsDiscordTokenProvider"/>. Requires the storage services.
    /// </summary>
    public static IServiceCollection AddDiscordSink(this IServiceCollection services, DiscordClientOptions? options = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(options ?? new DiscordClientOptions());
        services.TryAddSingleton<DiscordRateLimiter>();
        services.TryAddSingleton<IDiscordTokenProvider, SettingsDiscordTokenProvider>();
        services.AddHttpClient<DiscordRestClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddTransient<ISink, DiscordSink>();
        return services;
    }
}
