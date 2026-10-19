using System.Text.Json.Serialization;

namespace InformadoRSS.Import;

/// <summary>What the importer did with a MonitoRSS feed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedImportAction>))]
public enum FeedImportAction
{
    /// <summary>A new feed was created.</summary>
    [JsonStringEnumMemberName("created")]
    Created,

    /// <summary>A feed with the same URL already existed, here or earlier in the same export; its routes were added to it.</summary>
    [JsonStringEnumMemberName("merged")]
    Merged,

    /// <summary>The feed was not imported, e.g. because its URL is invalid.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
