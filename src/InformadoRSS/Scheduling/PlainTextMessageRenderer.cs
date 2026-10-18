using InformadoRSS.Core;

namespace InformadoRSS.Scheduling;

/// <summary>
/// Fallback renderer used until a template engine registers its own <see cref="IMessageRenderer"/>:
/// the item title in bold followed by its link. Route templates are ignored.
/// </summary>
public sealed class PlainTextMessageRenderer : IMessageRenderer
{
    public RenderedMessage Render(string? templateJson, FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lines = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            lines.Add($"**{item.Title.Trim()}**");
        }

        if (!string.IsNullOrWhiteSpace(item.Link))
        {
            lines.Add(item.Link.Trim());
        }

        return new RenderedMessage(lines.Count == 0 ? item.Id : string.Join('\n', lines));
    }
}
