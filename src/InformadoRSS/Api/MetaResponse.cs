namespace InformadoRSS.Api;

/// <summary>Static facts the web UI needs: available sinks, template placeholders and feed interval limits.</summary>
/// <param name="DefaultTemplateContent">Message text used by routes without a template.</param>
public sealed record MetaResponse(
    string Version,
    IReadOnlyList<string> Sinks,
    IReadOnlyList<string> Placeholders,
    string DefaultTemplateContent,
    long MinIntervalSeconds,
    long DefaultIntervalSeconds,
    long MaxIntervalSeconds);
