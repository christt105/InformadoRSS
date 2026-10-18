namespace InformadoRSS.Filters;

/// <summary>A problem in a filter definition, located by a JSON path such as <c>$.children[1].value</c>.</summary>
public sealed record FilterError(string Path, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Path}: {Message}";
}
