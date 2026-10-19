using InformadoRSS.Core;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Sinks.Ntfy;

public static class NtfySinkServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="NtfySink"/> as an <see cref="ISink"/> and the typed <see cref="NtfyClient"/>. The server comes
    /// from <see cref="NtfySettings"/>, whose settings are registered as definitions for the settings screen; until a URL is
    /// set, deliveries fail as temporary. Requires the storage services.
    /// </summary>
    public static IServiceCollection AddNtfySink(this IServiceCollection services)
    {
        services.AddHttpClient<NtfyClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.TryAddEnumerable(ServiceDescriptor.Transient<ISink, NtfySink>());
        services.AddSettingDefinition(new SettingDefinition(
            NtfySettings.UrlKey,
            "Server URL",
            "Base URL of the ntfy server used by ntfy destinations and alerts.",
            Group: "ntfy",
            Placeholder: "https://ntfy.sh"));
        services.AddSettingDefinition(new SettingDefinition(
            NtfySettings.TokenKey,
            "Access token",
            "Optional token for servers that require authentication, sent as a bearer token.",
            Secret: true,
            Group: "ntfy"));
        return services;
    }
}
