using System.Text.Json.Serialization;

namespace InformadoRSS.Filters;

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
