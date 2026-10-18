namespace InformadoRSS.Core;

/// <summary>Applies a route template to a feed item.</summary>
public interface IMessageRenderer
{
    /// <param name="templateJson">The route template as stored, or <c>null</c> for the default template.</param>
    RenderedMessage Render(string? templateJson, FeedItem item);
}
