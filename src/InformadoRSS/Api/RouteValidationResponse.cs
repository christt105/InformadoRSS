using InformadoRSS.Filters;
using InformadoRSS.Templates;

namespace InformadoRSS.Api;

/// <summary>Result of <c>POST /api/routes/validate</c>: the same checks as saving a route, without saving it.</summary>
public sealed record RouteValidationResponse(
    bool Valid,
    IReadOnlyList<ApiFieldError> Errors,
    IReadOnlyList<FilterError> FilterErrors,
    IReadOnlyList<TemplateError> TemplateErrors);
