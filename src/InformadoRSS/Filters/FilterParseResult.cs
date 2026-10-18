namespace InformadoRSS.Filters;

/// <summary>The outcome of <see cref="FilterJson.Parse"/>.</summary>
/// <param name="Root">The parsed tree, or <see langword="null"/> when the filter is absent or the JSON could not be read.</param>
/// <param name="Errors">Every problem found; empty when the filter is valid.</param>
public sealed record FilterParseResult(FilterNode? Root, IReadOnlyList<FilterError> Errors)
{
    /// <summary>Whether the filter can be compiled with <see cref="RouteFilter.Compile"/>.</summary>
    public bool IsValid => Errors.Count == 0;
}
