using InformadoRSS.Alerts;
using InformadoRSS.Core;
using InformadoRSS.Scheduling;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;
using InformadoRSS.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Tests;

public sealed class DeliveryRegistrationTests
{
    private sealed class PlainRenderer : IMessageRenderer
    {
        public RenderedMessage Render(string? templateJson, FeedItem item) => new(item.Title ?? string.Empty);
    }

    [Fact]
    public async Task AppRegistersTemplateRendererAndDiscordSink()
    {
        await using var factory = new AppFactory();

        Assert.IsType<TemplateMessageRenderer>(factory.Services.GetRequiredService<IMessageRenderer>());
        Assert.Contains(factory.Services.GetServices<ISink>(), sink => sink is DiscordSink { Name: "discord" });
        Assert.IsType<SettingsDiscordTokenProvider>(factory.Services.GetRequiredService<IDiscordTokenProvider>());
    }

    [Fact]
    public async Task AppRegistersNtfySinkAndAlertMonitor()
    {
        await using var factory = new AppFactory();
        using var scope = factory.Services.CreateScope();

        Assert.Single(scope.ServiceProvider.GetServices<ISink>(), sink => sink is NtfySink { Name: "ntfy" });
        Assert.IsType<AlertMonitor>(Assert.Single(scope.ServiceProvider.GetServices<IFeedCycleObserver>()));
        Assert.IsType<SqliteAlertStateStore>(scope.ServiceProvider.GetRequiredService<IAlertStateStore>());
    }

    [Fact]
    public void TemplateRendererWinsOverEarlierAndLaterDefaults()
    {
        var before = new ServiceCollection().AddSingleton<IMessageRenderer, PlainRenderer>().AddTemplates();
        var after = new ServiceCollection().AddTemplates();
        after.TryAddSingleton<IMessageRenderer, PlainRenderer>();

        foreach (var services in new[] { before, after })
        {
            using var provider = services.BuildServiceProvider();
            Assert.IsType<TemplateMessageRenderer>(provider.GetRequiredService<IMessageRenderer>());
            Assert.Single(provider.GetServices<IMessageRenderer>());
        }
    }
}
