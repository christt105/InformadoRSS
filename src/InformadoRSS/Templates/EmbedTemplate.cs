namespace InformadoRSS.Templates;

/// <summary>The embed part of a <see cref="MessageTemplate"/>. Text and URL fields accept placeholders.</summary>
/// <param name="Color">RGB color between <c>0</c> and <c>0xFFFFFF</c>, written as a decimal number in JSON.</param>
/// <param name="Image">URL of the large image, usually <c>{{image}}</c>.</param>
/// <param name="Thumbnail">URL of the small image in the corner.</param>
/// <param name="Timestamp">Whether to show the item's publication date.</param>
public sealed record EmbedTemplate(
    string? Title = null,
    string? Description = null,
    string? Url = null,
    int? Color = null,
    string? Image = null,
    string? Thumbnail = null,
    string? Footer = null,
    string? Author = null,
    bool Timestamp = false);
