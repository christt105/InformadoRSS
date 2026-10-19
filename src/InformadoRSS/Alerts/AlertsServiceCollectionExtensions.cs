using System.Globalization;
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
    /// the SQLite <see cref="IAlertStateStore"/>. Configuration lives in the settings store (see <see cref="AlertSettings"/>),
    /// with a definition per key for the settings screen.
    /// Requires the storage services, <see cref="DiscordSinkServiceCollectionExtensions.AddDiscordSink"/> and
    /// <see cref="NtfySinkServiceCollectionExtensions.AddNtfySink"/>.
    /// </summary>
    public static IServiceCollection AddAlerts(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAlertStateStore, SqliteAlertStateStore>();
        services.TryAddTransient<AlertSender>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IFeedCycleObserver, AlertMonitor>());
        services.AddSettingDefinition(new SettingDefinition(
            AlertSettings.TargetKey,
            "Send alerts to",
            "Where broken feed and destination alerts go: \"discord\" (default) or \"ntfy\".",
            Group: "Alerts",
            Placeholder: "discord"));
        services.AddSettingDefinition(new SettingDefinition(
            AlertSettings.DiscordChannelIdKey,
            "Discord channel id",
            "Channel for Discord alerts. When empty, alerts are sent as a direct message to the user below.",
            Group: "Alerts"));
        services.AddSettingDefinition(new SettingDefinition(
            AlertSettings.DiscordUserIdKey,
            "Discord user id",
            "User that gets Discord alerts as a direct message when no alert channel is set.",
            Group: "Alerts"));
        services.AddSettingDefinition(new SettingDefinition(
            AlertSettings.NtfyTopicKey,
            "ntfy topic",
            "Topic for ntfy alerts, on the server configured in the ntfy section.",
            Group: "Alerts"));
        services.AddSettingDefinition(new SettingDefinition(
            AlertSettings.FeedBrokenAfterMinutesKey,
            "Feed broken after (minutes)",
            "How long a feed may keep failing before it is reported as broken.",
            Group: "Alerts",
            Placeholder: ((int)AlertSettings.DefaultFeedBrokenAfter.TotalMinutes).ToString(CultureInfo.InvariantCulture)));
        return services;
    }
}
