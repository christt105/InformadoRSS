namespace InformadoRSS.Filters;

/// <summary>A group of nodes combined with a boolean operator.</summary>
public abstract class FilterGroup : FilterNode
{
    /// <summary>The nodes of the group, evaluated in order with short-circuiting.</summary>
    public IReadOnlyList<FilterNode> Children { get; init; } = [];
}
