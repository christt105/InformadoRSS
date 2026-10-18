using InformadoRSS.Core;

namespace InformadoRSS.Templates;

/// <summary>
/// Renders route templates (<see cref="TemplateJson"/>) and cuts the result to <see cref="MessageLimits"/>.
/// A stored template that cannot be parsed falls back to <see cref="MessageTemplate.Default"/>, and a message that
/// would be empty falls back to the item's title, link or id, so a delivery always has something to send.
/// </summary>
public sealed class TemplateMessageRenderer : IMessageRenderer
{
    public RenderedMessage Render(string? templateJson, FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var template = TemplateJson.Parse(templateJson).Template ?? MessageTemplate.Default;
        return Render(template, item);
    }

    public static RenderedMessage Render(MessageTemplate template, FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(item);

        var content = Text(template.Content, item, MessageLimits.Content);
        var embed = template.Embed is null ? null : RenderEmbed(template.Embed, item);

        if (content is null && embed is null && !ReferenceEquals(template, MessageTemplate.Default))
        {
            return Render(MessageTemplate.Default, item);
        }

        if (content is null && embed is null)
        {
            content = MessageLimits.Truncate(item.Title ?? item.Link ?? item.Id, MessageLimits.Content);
        }

        return new RenderedMessage(content ?? string.Empty, embed);
    }

    internal static bool IsWebUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static RenderedEmbed? RenderEmbed(EmbedTemplate template, FeedItem item)
    {
        var title = Text(template.Title, item, MessageLimits.EmbedTitle);
        var description = Text(template.Description, item, MessageLimits.EmbedDescription);
        var footer = Text(template.Footer, item, MessageLimits.EmbedFooter);
        var author = Text(template.Author, item, MessageLimits.EmbedAuthor);
        var image = Url(template.Image, item);
        var thumbnail = Url(template.Thumbnail, item);

        if (title is null && description is null && footer is null && author is null && image is null && thumbnail is null)
        {
            return null;
        }

        var overflow = (title?.Length ?? 0) + (description?.Length ?? 0) + (footer?.Length ?? 0) + (author?.Length ?? 0)
            - MessageLimits.EmbedTotal;
        if (overflow > 0 && description is not null)
        {
            description = description.Length - overflow > 1
                ? MessageLimits.Truncate(description, description.Length - overflow)
                : null;
        }

        return new RenderedEmbed(
            Title: title,
            Description: description,
            Url: Url(template.Url, item),
            Color: template.Color is >= 0 and <= TemplateValidator.MaxColor ? template.Color : null,
            ImageUrl: image,
            ThumbnailUrl: thumbnail,
            Footer: footer,
            Author: author,
            Timestamp: template.Timestamp ? item.Published : null);
    }

    private static string? Text(string? template, FeedItem item, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var text = TemplatePlaceholders.Expand(template, item).Trim();
        return text.Length == 0 ? null : MessageLimits.Truncate(text, maxLength);
    }

    private static string? Url(string? template, FeedItem item)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var url = TemplatePlaceholders.Expand(template, item).Trim();
        return IsWebUrl(url) ? url : null;
    }
}
