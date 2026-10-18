using System.Text.Json.Serialization;

namespace InformadoRSS.Filters;

/// <summary>The <see cref="Core.FeedItem"/> field a <see cref="FilterCondition"/> tests.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FilterField>))]
public enum FilterField
{
    /// <summary>The item title.</summary>
    [JsonStringEnumMemberName("title")]
    Title,

    /// <summary>The plain text description.</summary>
    [JsonStringEnumMemberName("description")]
    Description,

    /// <summary>The item author.</summary>
    [JsonStringEnumMemberName("author")]
    Author,

    /// <summary>The item link.</summary>
    [JsonStringEnumMemberName("link")]
    Link,

    /// <summary>The item categories; the condition matches when any of them matches.</summary>
    [JsonStringEnumMemberName("category")]
    Category,
}
