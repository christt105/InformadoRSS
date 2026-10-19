using System.Globalization;
using System.Text.Json;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Sources;
using InformadoRSS.Storage;
using InformadoRSS.Templates;

namespace InformadoRSS.Import;

/// <summary>
/// Command line entry points, run instead of the server:
/// <c>import-monitorss &lt;file&gt; [--test-channel &lt;id&gt; [--test-guild &lt;id&gt;]] [--dry-run]</c> and
/// <c>send-latest [--channel &lt;id&gt;] [--feed &lt;id&gt;] [--include-disabled]</c>.
/// They use the same database as the server (<c>Database__Path</c>).
/// </summary>
public static class ImportCommand
{
    public const string ImportName = "import-monitorss";
    public const string SendLatestName = "send-latest";

    private const string Usage = """
        Usage:
          InformadoRSS import-monitorss <file> [--test-channel <id> [--test-guild <id>]] [--dry-run]
          InformadoRSS send-latest [--channel <id>] [--feed <id>] [--include-disabled]
        """;

    public static bool Handles(string command) => command is ImportName or SendLatestName;

    /// <returns>0 on success, 1 when the work failed, 2 for invalid arguments.</returns>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || !Handles(args[0]))
        {
            return await UsageErrorAsync(null);
        }

        string? file = null;
        string? channel = null;
        string? guild = null;
        long? feedId = null;
        var dryRun = false;
        var includeDisabled = false;
        var import = args[0] == ImportName;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--test-channel" when import && i + 1 < args.Length:
                case "--channel" when !import && i + 1 < args.Length:
                    channel = args[++i];
                    break;
                case "--test-guild" when import && i + 1 < args.Length:
                    guild = args[++i];
                    break;
                case "--dry-run" when import:
                    dryRun = true;
                    break;
                case "--feed" when !import && i + 1 < args.Length:
                    if (!long.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                    {
                        return await UsageErrorAsync($"Invalid feed id: {args[i]}");
                    }

                    feedId = id;
                    break;
                case "--include-disabled" when !import:
                    includeDisabled = true;
                    break;
                case var value when import && file is null && !value.StartsWith("--", StringComparison.Ordinal):
                    file = value;
                    break;
                default:
                    return await UsageErrorAsync($"Unexpected argument: {args[i]}");
            }
        }

        if (import && file is null)
        {
            return await UsageErrorAsync("Missing the export file.");
        }

        if (channel is not null && !SnowflakeComparer.IsSnowflake(channel))
        {
            return await UsageErrorAsync($"Invalid Discord channel id: {channel}");
        }

        if (guild is not null && (channel is null || !SnowflakeComparer.IsSnowflake(guild)))
        {
            return await UsageErrorAsync($"--test-guild needs a Discord id and --test-channel: {guild}");
        }

        using var host = BuildHost();
        await host.Services.MigrateDatabaseAsync(cancellationToken);

        if (import)
        {
            IReadOnlyList<MonitorssFeed> feeds;
            try
            {
                await using var stream = File.OpenRead(file!);
                feeds = await MonitorssExportReader.ReadAsync(stream, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                await Console.Error.WriteLineAsync($"Cannot read {file}: {ex.Message}");
                return 1;
            }

            var report = await host.Services.GetRequiredService<MonitorssImporter>()
                .ImportAsync(feeds, new ImportOptions(channel, dryRun, guild), cancellationToken);
            await Console.Out.WriteAsync(ImportReportFormatter.Format(report));
            return 0;
        }

        var results = await host.Services.GetRequiredService<LatestItemSender>()
            .SendAsync(new SendLatestOptions(channel, feedId, includeDisabled), cancellationToken);
        await Console.Out.WriteAsync(ImportReportFormatter.Format(results));
        return results.All(result => result.Status is SendLatestStatus.Sent or SendLatestStatus.NoMatchingItem) ? 0 : 1;
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddStorage();
        builder.Services.AddRssAtomSource();
        builder.Services.AddTemplates();
        builder.Services.AddDiscordSink();
        builder.Services.AddNtfySink();
        builder.Services.AddMonitorssImport();
        return builder.Build();
    }

    private static async Task<int> UsageErrorAsync(string? message)
    {
        if (message is not null)
        {
            await Console.Error.WriteLineAsync(message);
        }

        await Console.Error.WriteLineAsync(Usage);
        return 2;
    }
}
