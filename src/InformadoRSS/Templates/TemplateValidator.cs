namespace InformadoRSS.Templates;

/// <summary>Checks a parsed <see cref="MessageTemplate"/> for problems the JSON shape alone cannot catch.</summary>
public static class TemplateValidator
{
    public const int MaxColor = 0xFFFFFF;

    public static IReadOnlyList<TemplateError> Validate(MessageTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        List<TemplateError> errors = [];
        var embed = template.Embed;

        if (string.IsNullOrWhiteSpace(template.Content) && embed is null)
        {
            errors.Add(new TemplateError("$", "the template needs \"content\", an \"embed\" or both"));
        }

        CheckText(errors, "$.content", template.Content);

        if (embed is null)
        {
            return errors;
        }

        if (new[] { embed.Title, embed.Description, embed.Image, embed.Thumbnail, embed.Footer, embed.Author }.All(string.IsNullOrWhiteSpace))
        {
            errors.Add(new TemplateError(
                "$.embed",
                "the embed needs at least a title, description, image, thumbnail, footer or author"));
        }

        CheckText(errors, "$.embed.title", embed.Title);
        CheckText(errors, "$.embed.description", embed.Description);
        CheckText(errors, "$.embed.footer", embed.Footer);
        CheckText(errors, "$.embed.author", embed.Author);
        CheckUrl(errors, "$.embed.url", embed.Url);
        CheckUrl(errors, "$.embed.image", embed.Image);
        CheckUrl(errors, "$.embed.thumbnail", embed.Thumbnail);

        if (embed.Color is < 0 or > MaxColor)
        {
            errors.Add(new TemplateError("$.embed.color", $"the color must be between 0 and {MaxColor} (0xFFFFFF)"));
        }

        return errors;
    }

    private static void CheckText(List<TemplateError> errors, string path, string? text)
    {
        if (text is null)
        {
            return;
        }

        foreach (var token in TemplatePlaceholders.FindUnknown(text).Distinct(StringComparer.Ordinal))
        {
            errors.Add(new TemplateError(
                path,
                $"unknown placeholder {token}; available: {string.Join(", ", TemplatePlaceholders.Supported)}"));
        }
    }

    private static void CheckUrl(List<TemplateError> errors, string path, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CheckText(errors, path, text);

        if (!TemplatePlaceholders.ContainsToken(text) && !TemplateMessageRenderer.IsWebUrl(text.Trim()))
        {
            errors.Add(new TemplateError(path, "must be an http(s) URL or a placeholder such as {{link}} or {{image}}"));
        }
    }
}
