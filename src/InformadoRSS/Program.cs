using InformadoRSS;

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

var builder = WebApplication.CreateSlimBuilder(args);

if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(builder.Configuration["http_ports"]))
{
    builder.WebHost.UseUrls($"http://+:{AppInfo.DefaultPort}");
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => new HealthResponse("ok", AppInfo.Version));

app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
