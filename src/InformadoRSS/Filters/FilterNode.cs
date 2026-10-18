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
