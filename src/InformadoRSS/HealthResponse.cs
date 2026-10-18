namespace InformadoRSS;

/// <summary>Body returned by the <c>/health</c> endpoint.</summary>
public sealed record HealthResponse(string Status, string Version);
