using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Import;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(ImportReport))]
[JsonSerializable(typeof(IReadOnlyList<SendLatestResult>))]
[JsonSerializable(typeof(ImportEndpointError))]
internal sealed partial class ImportJsonContext : JsonSerializerContext;
