using System.Net;
using InformadoRSS.Storage;
using Microsoft.AspNetCore.Mvc;

namespace InformadoRSS.Api;

public static class SettingsEndpoints
{
    public const int MaxKeyLength = 100;
    public const int MaxValueLength = 4000;

    private static readonly string[] SecretMarkers = ["token", "password", "secret", "apikey", "api_key"];

    /// <summary>
    /// Maps <c>GET /api/settings</c>, which lists every defined or stored setting without revealing secrets, and
    /// <c>PUT /api/settings</c>, which takes an object of keys to new values (<see langword="null"/> or empty removes a key)
    /// and leaves keys it does not mention unchanged.
    /// </summary>
    public static IEndpointRouteBuilder MapSettingsApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings", ListAsync);
        endpoints.MapPut("/api/settings", UpdateAsync);
        return endpoints;
    }

    /// <summary>
    /// Whether the value of <paramref name="key"/> must never be returned: either its definition says so, or the key
    /// looks like a credential.
    /// </summary>
    public static bool IsSecret(string key, SettingDefinition? definition) =>
        definition?.Secret == true || SecretMarkers.Any(marker => key.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static async Task<IResult> ListAsync(
        [FromServices] ISettingsStore settings,
        [FromServices] IEnumerable<SettingDefinition> definitions,
        CancellationToken cancellationToken) =>
        await JsonAsync(settings, definitions, cancellationToken);

    private static async Task<IResult> UpdateAsync(
        HttpRequest request,
        [FromServices] ISettingsStore settings,
        [FromServices] IEnumerable<SettingDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var (body, invalid) = await ApiResults.ReadBodyAsync(
            request, ApiJsonContext.Default.DictionaryStringString, "{\"discord.botToken\":\"...\"}", cancellationToken);
        if (body is null)
        {
            return invalid!;
        }

        var errors = new List<ApiFieldError>();
        foreach (var (key, value) in body)
        {
            if (!IsValidKey(key))
            {
                errors.Add(new ApiFieldError(
                    "$", $"invalid key \"{key}\"; keys use letters, digits, '.', '_' and '-', up to {MaxKeyLength} characters"));
            }
            else if (value is { Length: > MaxValueLength })
            {
                errors.Add(new ApiFieldError($"$['{key}']", $"the value must be at most {MaxValueLength} characters"));
            }
        }

        if (errors.Count > 0)
        {
            return ApiResults.Error(HttpStatusCode.BadRequest, new ApiError("invalid_settings", "the settings are not valid", errors));
        }

        foreach (var (key, value) in body)
        {
            var trimmed = value?.Trim();
            await settings.SetAsync(key, string.IsNullOrEmpty(trimmed) ? null : trimmed, cancellationToken);
        }

        return await JsonAsync(settings, definitions, cancellationToken);
    }

    private static async Task<IResult> JsonAsync(
        ISettingsStore settings, IEnumerable<SettingDefinition> definitions, CancellationToken cancellationToken)
    {
        var stored = await settings.ListAsync(cancellationToken);
        var list = new List<SettingResponse>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (known.Add(definition.Key))
            {
                list.Add(ToResponse(definition.Key, definition, stored.GetValueOrDefault(definition.Key)));
            }
        }

        foreach (var (key, value) in stored.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!known.Contains(key))
            {
                list.Add(ToResponse(key, null, value));
            }
        }

        return TypedResults.Json<IReadOnlyList<SettingResponse>>(list, ApiJsonContext.Default.IReadOnlyListSettingResponse);
    }

    private static SettingResponse ToResponse(string key, SettingDefinition? definition, string? value)
    {
        var secret = IsSecret(key, definition);
        return new SettingResponse(
            key,
            definition?.Label ?? key,
            definition?.Description ?? string.Empty,
            definition?.Group ?? "Other",
            definition?.Placeholder,
            secret,
            value is not null,
            secret ? null : value,
            definition is not null);
    }

    private static bool IsValidKey(string key) =>
        key.Length is > 0 and <= MaxKeyLength
        && char.IsAsciiLetterOrDigit(key[0])
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
