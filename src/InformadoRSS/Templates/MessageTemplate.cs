namespace InformadoRSS.Templates;

/// <summary>
/// A route template: message text and an optional embed, both with <c>{{placeholder}}</c> tokens.
/// See <see cref="TemplatePlaceholders"/> for the supported tokens.
/// </summary>
public sealed record MessageTemplate(string? Content = null, EmbedTemplate? Embed = null)
{
    /// <summary>Used when a route has no template: the title in bold and the link below, like MonitoRSS.</summary>
    public static MessageTemplate Default { get; } = new("**{{title}}**\n{{link}}");
}
