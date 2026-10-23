using InformadoRSS.Filters;
using InformadoRSS.Templates;

namespace InformadoRSS.Api;

/// <summary>Error body of the application API.</summary>
/// <param name="Error">
/// A stable code: <c>invalid_body</c>, <c>invalid_feed</c>, <c>invalid_route</c>, <c>invalid_settings</c>,
/// <c>duplicate_feed</c>, <c>invalid_request</c>, <c>not_found</c>, <c>fetch_failed</c> or <c>render_failed</c>.
/// </param>
/// <param name="Message">A human-readable explanation.</param>
/// <param name="Errors">Problems with individual fields of the request.</param>
/// <param name="FilterErrors">For <c>invalid_route</c>, every problem found in the filter.</param>
/// <param name="TemplateErrors">For <c>invalid_route</c>, every problem found in the template.</param>
public sealed record ApiError(
    string Error,
    string Message,
    IReadOnlyList<ApiFieldError>? Errors = null,
    IReadOnlyList<FilterError>? FilterErrors = null,
    IReadOnlyList<TemplateError>? TemplateErrors = null);
