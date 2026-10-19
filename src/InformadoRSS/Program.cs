using InformadoRSS;
using InformadoRSS.Alerts;
using InformadoRSS.Import;
using InformadoRSS.Scheduling;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Sources;
using InformadoRSS.Storage;
using InformadoRSS.Templates;

if (args.Length > 0 && args[0] == HealthCheckCommand.Flag)
{
    Uri? url = HealthCheckCommand.DefaultUrl;
    if (args.Length > 1 && !Uri.TryCreate(args[1], UriKind.Absolute, out url))
    {
        await Console.Error.WriteLineAsync($"Invalid health check URL: {args[1]}");
        return 1;
    }

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    return await HealthCheckCommand.RunAsync(client, url);
}

if (args.Length > 0 && ImportCommand.Handles(args[0]))
{
    return await ImportCommand.RunAsync(args);
}

var builder = WebApplication.CreateSlimBuilder(args);

if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(builder.Configuration["http_ports"]))
{
    builder.WebHost.UseUrls($"http://+:{AppInfo.DefaultPort}");
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

builder.Services.AddStorage();
builder.Services.AddRssAtomSource();
builder.Services.AddTemplates();
builder.Services.AddDiscordSink();
builder.Services.AddDiscordDirectory();
builder.Services.AddNtfySink();
builder.Services.AddScheduler();
builder.Services.AddAlerts();
builder.Services.AddMonitorssImport();

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => new HealthResponse("ok", AppInfo.Version));
app.MapDiscordApi();
app.MapImportApi();

app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
