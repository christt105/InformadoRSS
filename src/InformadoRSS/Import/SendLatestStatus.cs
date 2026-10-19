using System.Text.Json.Serialization;

namespace InformadoRSS.Import;

/// <summary>The outcome of force-sending the latest item of a route.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SendLatestStatus>))]
public enum SendLatestStatus
{
    /// <summary>Every destination accepted the message.</summary>
    [JsonStringEnumMemberName("sent")]
    Sent,

    /// <summary>At least one destination did not accept the message.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>No item of the feed passes the route filter.</summary>
    [JsonStringEnumMemberName("noMatchingItem")]
    NoMatchingItem,

    /// <summary>The feed could not be fetched.</summary>
    [JsonStringEnumMemberName("fetchFailed")]
    FetchFailed,

    /// <summary>The stored route filter is invalid.</summary>
    [JsonStringEnumMemberName("invalidFilter")]
    InvalidFilter,
}
