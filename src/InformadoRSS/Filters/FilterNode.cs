using System.Text.Json.Serialization;

namespace InformadoRSS.Filters;

/// <summary>A node of a route filter tree: either a group (<see cref="AllGroup"/>, <see cref="AnyGroup"/>) or a <see cref="FilterCondition"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AllGroup), "all")]
[JsonDerivedType(typeof(AnyGroup), "any")]
[JsonDerivedType(typeof(FilterCondition), "condition")]
public abstract class FilterNode
{
    /// <summary>Inverts the result of this node.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Negate { get; init; }
}

/// <summary>A group of nodes combined with a boolean operator.</summary>
public abstract class FilterGroup : FilterNode
{
    /// <summary>The nodes of the group, evaluated in order with short-circuiting.</summary>
    public IReadOnlyList<FilterNode> Children { get; init; } = [];
}

/// <summary>Matches when every child matches (AND). An empty group matches.</summary>
public sealed class AllGroup : FilterGroup;

/// <summary>Matches when at least one child matches (OR). An empty group does not match.</summary>
public sealed class AnyGroup : FilterGroup;

/// <summary>A single test of one item field against a value.</summary>
public sealed class FilterCondition : FilterNode
{
    /// <summary>The item field to test.</summary>
    public required FilterField Field { get; init; }

    /// <summary>For <see cref="FilterField.Category"/> only: restricts the test to categories with this domain (e.g. <c>type</c>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Domain { get; init; }

    /// <summary>How the field is compared with <see cref="Value"/>.</summary>
    public required FilterOperator Operator { get; init; }

    /// <summary>The text to compare with, or the pattern for <see cref="FilterOperator.Regex"/>.</summary>
    public required string Value { get; init; }

    /// <summary>Compares with exact casing. Comparisons ignore case by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CaseSensitive { get; init; }
}

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
