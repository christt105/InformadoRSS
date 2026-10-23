using System.Text.Json;
using System.Text.Json.Serialization;

namespace InformadoRSS.Api;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(FeedRequest))]
[JsonSerializable(typeof(FeedResponse))]
[JsonSerializable(typeof(IReadOnlyList<FeedResponse>))]
[JsonSerializable(typeof(FeedValidationRequest))]
[JsonSerializable(typeof(FeedValidationResponse))]
[JsonSerializable(typeof(FeedItemsResponse))]
[JsonSerializable(typeof(ItemResetRequest))]
[JsonSerializable(typeof(ItemResetResponse))]
[JsonSerializable(typeof(ItemSendRequest))]
[JsonSerializable(typeof(ItemSendResponse))]
[JsonSerializable(typeof(RouteRequest))]
[JsonSerializable(typeof(RouteResponse))]
[JsonSerializable(typeof(RouteValidationResponse))]
[JsonSerializable(typeof(IReadOnlyList<SettingResponse>))]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(MetaResponse))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
