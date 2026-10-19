using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.HttpResults;

namespace InformadoRSS.Api;

/// <summary>Shared helpers of the application API endpoints: error responses and request body reading.</summary>
internal static class ApiResults
{
    public static JsonHttpResult<ApiError> Error(HttpStatusCode status, ApiError error) =>
        TypedResults.Json(error, ApiJsonContext.Default.ApiError, statusCode: (int)status);

    public static JsonHttpResult<ApiError> NotFound(string message) =>
        Error(HttpStatusCode.NotFound, new ApiError("not_found", message));

    /// <summary>
    /// Reads a JSON body. Returns a <c>400 invalid_body</c> result instead of a value when the body is missing,
    /// not JSON, <c>null</c> or of the wrong shape.
    /// </summary>
    public static async Task<(T? Value, IResult? Error)> ReadBodyAsync<T>(
        HttpRequest request, JsonTypeInfo<T> typeInfo, string example, CancellationToken cancellationToken)
        where T : class
    {
        var invalid = Error(
            HttpStatusCode.BadRequest,
            new ApiError("invalid_body", $"the body must be a JSON object such as {example}"));
        if (!request.HasJsonContentType())
        {
            return (null, invalid);
        }

        try
        {
            var value = await request.ReadFromJsonAsync(typeInfo, cancellationToken);
            return value is null ? (null, invalid) : (value, null);
        }
        catch (JsonException)
        {
            return (null, invalid);
        }
    }
}
