using System.Text.Json.Serialization;

namespace InformadoRSS.Filters;

/// <summary>The comparison a <see cref="FilterCondition"/> performs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FilterOperator>))]
public enum FilterOperator
{
    /// <summary>The field contains the value.</summary>
    [JsonStringEnumMemberName("contains")]
    Contains,

    /// <summary>The field is exactly the value.</summary>
    [JsonStringEnumMemberName("equals")]
    Equal,

    /// <summary>The field starts with the value.</summary>
    [JsonStringEnumMemberName("startsWith")]
    StartsWith,

    /// <summary>The value is a .NET regular expression that matches somewhere in the field.</summary>
    [JsonStringEnumMemberName("regex")]
    Regex,
}
