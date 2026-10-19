using InformadoRSS.Templates;

namespace InformadoRSS.Import;

/// <summary>The outcome of <see cref="MonitorssTemplateTranslator.Translate"/>.</summary>
/// <param name="Template">The translated template, or <c>null</c> for the default template.</param>
/// <param name="Notes">Placeholders that were removed or render slightly differently.</param>
public sealed record TemplateTranslation(MessageTemplate? Template, IReadOnlyList<string> Notes);
