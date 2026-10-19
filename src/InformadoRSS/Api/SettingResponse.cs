namespace InformadoRSS.Api;

/// <summary>A setting as listed by <c>GET /api/settings</c>.</summary>
/// <param name="Value">The stored value; always <see langword="null"/> for secrets.</param>
/// <param name="IsSet">Whether a value is stored.</param>
/// <param name="Defined">Whether a module registered a <c>SettingDefinition</c> for the key.</param>
public sealed record SettingResponse(
    string Key,
    string Label,
    string Description,
    string Group,
    string? Placeholder,
    bool Secret,
    bool IsSet,
    string? Value,
    bool Defined);
