namespace InformadoRSS.Import;

/// <summary>Error body of the <c>/api/import</c> endpoints.</summary>
/// <param name="Error">A stable code: <c>invalid_document</c> or <c>invalid_id</c>.</param>
public sealed record ImportEndpointError(string Error, string Message);
