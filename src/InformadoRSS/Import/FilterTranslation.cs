using InformadoRSS.Filters;

namespace InformadoRSS.Import;

/// <summary>The outcome of <see cref="MonitorssFilterTranslator.Translate"/>.</summary>
/// <param name="Root">The translated filter; <c>null</c> when every item passes or when the filter could not be translated.</param>
/// <param name="Problems">Parts that could not be translated. When not empty, <paramref name="Root"/> is <c>null</c>.</param>
/// <param name="Notes">Parts translated with a small difference in behavior.</param>
public sealed record FilterTranslation(FilterNode? Root, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes)
{
    public bool IsTranslated => Problems.Count == 0;
}
