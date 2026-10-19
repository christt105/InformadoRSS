namespace InformadoRSS.Api;

/// <summary>A problem with one field of a request body, located by a JSON path such as <c>$.destinations[0].sink</c>.</summary>
public sealed record ApiFieldError(string Path, string Message);
