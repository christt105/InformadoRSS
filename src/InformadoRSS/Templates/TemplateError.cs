namespace InformadoRSS.Templates;

/// <summary>A problem in a template definition, located by a JSON path such as <c>$.embed.color</c>.</summary>
public sealed record TemplateError(string Path, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Path}: {Message}";
}
