using System.Globalization;
using InformadoRSS.Alerts;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Backups;

public static class BackupsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="BackupRunner"/> and the <see cref="BackupService"/> that drives it, with a setting
    /// definition per key (see <see cref="BackupSettings"/>). Requires the storage services and
    /// <see cref="AlertsServiceCollectionExtensions.AddAlerts"/>.
    /// </summary>
    public static IServiceCollection AddBackups(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<BackupRunner>();
        services.AddHostedService<BackupService>();
        services.AddSettingDefinition(new SettingDefinition(
            BackupSettings.DirectoryKey,
            "Backup folder",
            "Folder inside the container where copies of the database are written, usually a mounted volume. Empty disables backups.",
            Group: "Backups",
            Placeholder: "/backups"));
        services.AddSettingDefinition(new SettingDefinition(
            BackupSettings.IntervalHoursKey,
            "Backup every (hours)",
            "Time between two backups. A failed backup is retried every 15 minutes.",
            Group: "Backups",
            Placeholder: ((int)BackupSettings.DefaultInterval.TotalHours).ToString(CultureInfo.InvariantCulture)));
        services.AddSettingDefinition(new SettingDefinition(
            BackupSettings.KeepKey,
            "Backups to keep",
            "How many backup files to keep; the oldest are deleted after each new backup.",
            Group: "Backups",
            Placeholder: BackupSettings.DefaultKeep.ToString(CultureInfo.InvariantCulture)));
        return services;
    }
}
