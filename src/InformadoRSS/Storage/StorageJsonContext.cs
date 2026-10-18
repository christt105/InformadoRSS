using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Storage;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConfigurationSnapshot))]
internal sealed partial class StorageJsonContext : JsonSerializerContext;
