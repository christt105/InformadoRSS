namespace InformadoRSS.Storage;

/// <summary>
/// Describes a setting so the web UI can show it before it has a value. Modules register theirs with
/// <see cref="StorageServiceCollectionExtensions.AddSettingDefinition"/>; the settings screen lists them in registration order.
/// </summary>
/// <param name="Key">The <see cref="ISettingsStore"/> key.</param>
/// <param name="Label">Short name shown next to the input.</param>
/// <param name="Description">Help text shown under the input.</param>
/// <param name="Secret">Whether the value is write-only: the API only tells whether it is set, never returns it.</param>
/// <param name="Group">Heading the setting is listed under, e.g. <c>Discord</c>.</param>
/// <param name="Placeholder">Example value shown in an empty input.</param>
public sealed record SettingDefinition(
    string Key,
    string Label,
    string Description,
    bool Secret = false,
    string Group = "General",
    string? Placeholder = null);
