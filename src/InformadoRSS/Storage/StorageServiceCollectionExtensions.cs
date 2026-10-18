namespace InformadoRSS.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite database and stores. The file path is read from <see cref="SqliteDatabase.PathConfigKey"/>
    /// (environment variable <c>Database__Path</c>) and defaults to <see cref="SqliteDatabase.DefaultPath"/>.
    /// </summary>
    public static IServiceCollection AddStorage(this IServiceCollection services)
    {
        services.AddSingleton(provider => SqliteDatabase.FromPath(
            provider.GetRequiredService<IConfiguration>()[SqliteDatabase.PathConfigKey] is { Length: > 0 } path
                ? path
                : SqliteDatabase.DefaultPath));
        services.AddSingleton<IFeedStore, SqliteFeedStore>();
        services.AddSingleton<IRouteStore, SqliteRouteStore>();
        services.AddSingleton<ISeenItemStore, SqliteSeenItemStore>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<ConfigurationTransfer>();
        return services;
    }

    /// <summary>Brings the database schema up to date. Call once at startup before serving requests.</summary>
    public static Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default) =>
        SchemaMigrator.MigrateAsync(services.GetRequiredService<SqliteDatabase>(), cancellationToken);
}
