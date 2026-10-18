using InformadoRSS.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Scheduling;

public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the feed scheduler and its background service. Options come from the <c>Scheduler</c> configuration
    /// section (see <see cref="SchedulerOptions"/>). Sinks are taken from every registered <see cref="ISink"/>; a
    /// <see cref="PlainTextMessageRenderer"/> is registered only if no <see cref="IMessageRenderer"/> is registered.
    /// </summary>
    public static IServiceCollection AddScheduler(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => SchedulerOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.TryAddSingleton<IMessageRenderer, PlainTextMessageRenderer>();
        services.TryAddTransient<FeedProcessor>();
        services.TryAddSingleton<FeedScheduler>();
        services.AddHostedService<SchedulerService>();
        return services;
    }
}
