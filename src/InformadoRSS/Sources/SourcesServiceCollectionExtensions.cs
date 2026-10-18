using System.Net;
using InformadoRSS.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Sources;

public static class SourcesServiceCollectionExtensions
{
    /// <summary>Registers <see cref="RssAtomSource"/> as the <see cref="ISource"/> implementation, with its own typed <see cref="HttpClient"/>.</summary>
    public static IServiceCollection AddRssAtomSource(this IServiceCollection services, RssAtomSourceOptions? options = null)
    {
        services.TryAddSingleton(options ?? new RssAtomSourceOptions());
        services.AddHttpClient<ISource, RssAtomSource>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                MaxAutomaticRedirections = 10,
            });
        return services;
    }
}
