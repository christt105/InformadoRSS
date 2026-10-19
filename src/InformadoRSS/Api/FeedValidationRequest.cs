namespace InformadoRSS.Api;

/// <summary>Body of <c>POST /api/feeds/validate</c>.</summary>
public sealed record FeedValidationRequest(string? Url);
