using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Import;

public static class ImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MonitorssImporter"/> and <see cref="LatestItemSender"/>. Requires the storage services, an
    /// <see cref="Core.ISource"/>, an <see cref="Core.IMessageRenderer"/> and the sinks.
    /// </summary>
    public static IServiceCollection AddMonitorssImport(this IServiceCollection services)
    {
        services.TryAddTransient<MonitorssImporter>();
        services.TryAddTransient<LatestItemSender>();
        return services;
    }
}
