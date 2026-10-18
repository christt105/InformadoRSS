using System.Text.Json.Serialization;

namespace InformadoRSS.Storage;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ColumnJsonContext : JsonSerializerContext;
