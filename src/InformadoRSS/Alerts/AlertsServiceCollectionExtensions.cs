using InformadoRSS.Scheduling;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Alerts;

public static class AlertsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="AlertMonitor"/> as an <see cref="IFeedCycleObserver"/>, the <see cref="AlertSender"/> and
    /// the SQLite <see cref="IAlertStateStore"/>. Configuration lives in the settings store (see <see cref="AlertSettings"/>).
    /// Requires the storage services, <see cref="DiscordSinkServiceCollectionExtensions.AddDiscordSink"/> and
    /// <see cref="NtfySinkServiceCollectionExtensions.AddNtfySink"/>.
    /// </summary>
    public static IServiceCollection AddAlerts(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAlertStateStore, SqliteAlertStateStore>();
        services.TryAddTransient<AlertSender>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IFeedCycleObserver, AlertMonitor>());
        return services;
    }
}
