namespace InformadoRSS.Core;

/// <summary>A rendered rich card. Every field is optional; empty fields are <see langword="null"/>.</summary>
/// <param name="Color">An RGB color such as <c>0x5865F2</c>.</param>
public sealed record RenderedEmbed(
    string? Title = null,
    string? Description = null,
    string? Url = null,
    int? Color = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null,
    string? Footer = null,
    string? Author = null,
    DateTimeOffset? Timestamp = null);
