namespace InformadoRSS.Templates;

/// <summary>The outcome of <see cref="TemplateJson.Parse"/>.</summary>
/// <param name="Template">
/// The parsed template, or <see langword="null"/> when it is absent (the default applies) or the JSON could not be read.
/// </param>
/// <param name="Errors">Every problem found; empty when the template is valid.</param>
public sealed record TemplateParseResult(MessageTemplate? Template, IReadOnlyList<TemplateError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
