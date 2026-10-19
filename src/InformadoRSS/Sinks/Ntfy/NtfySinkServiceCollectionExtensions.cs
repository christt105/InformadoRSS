using InformadoRSS.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Sinks.Ntfy;

public static class NtfySinkServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="NtfySink"/> as an <see cref="ISink"/> and the typed <see cref="NtfyClient"/>. The server comes
    /// from <see cref="NtfySettings"/>; until a URL is set, deliveries fail as temporary. Requires the storage services.
    /// </summary>
    public static IServiceCollection AddNtfySink(this IServiceCollection services)
    {
        services.AddHttpClient<NtfyClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.TryAddEnumerable(ServiceDescriptor.Transient<ISink, NtfySink>());
        return services;
    }
}
