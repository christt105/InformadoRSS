using InformadoRSS.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InformadoRSS.Tests;

/// <summary>Runs the app against a throwaway database file.</summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("informadorss-data-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting(SqliteDatabase.PathConfigKey, Path.Combine(_dataDirectory, "informadorss.db"))
            .UseSetting("Scheduler:Enabled", "false");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_dataDirectory, recursive: true);
    }
}
