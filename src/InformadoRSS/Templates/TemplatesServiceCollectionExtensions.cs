using InformadoRSS.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InformadoRSS.Templates;

public static class TemplatesServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TemplateMessageRenderer"/> as the <see cref="IMessageRenderer"/>, replacing any renderer
    /// registered before; renderers added later with <c>TryAdd</c> are ignored.
    /// </summary>
    public static IServiceCollection AddTemplates(this IServiceCollection services)
    {
        services.RemoveAll<IMessageRenderer>();
        services.AddSingleton<IMessageRenderer, TemplateMessageRenderer>();
        return services;
    }
}
