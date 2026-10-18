using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Sinks.Discord;

public static class DiscordDirectoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DiscordDirectory"/> and its shared <see cref="DiscordDirectoryCache"/>. Requires
    /// <see cref="DiscordSinkServiceCollectionExtensions.AddDiscordSink"/> for the REST client and token provider.
    /// </summary>
    public static IServiceCollection AddDiscordDirectory(this IServiceCollection services, DiscordDirectoryOptions? options = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(options ?? new DiscordDirectoryOptions());
        services.TryAddSingleton<DiscordDirectoryCache>();
        services.TryAddTransient<DiscordDirectory>();
        return services;
    }
}
