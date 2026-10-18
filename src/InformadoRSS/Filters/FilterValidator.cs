namespace InformadoRSS.Filters;

/// <summary>Checks a filter tree for problems that would make it fail or behave surprisingly.</summary>
public static class FilterValidator
{
    /// <summary>Returns every problem in the tree; an empty list means it is valid. An absent filter is valid.</summary>
    public static IReadOnlyList<FilterError> Validate(FilterNode? root)
    {
        var errors = new List<FilterError>();
        if (root is not null)
        {
            Visit(root, "$", isRoot: true, errors);
        }

        return errors;
    }

    private static void Visit(FilterNode? node, string path, bool isRoot, List<FilterError> errors)
    {
        switch (node)
        {
            case null:
                errors.Add(new FilterError(path, "node is missing"));
                break;
            case FilterGroup group:
                VisitGroup(group, path, isRoot, errors);
                break;
            case FilterCondition condition:
                VisitCondition(condition, path, errors);
                break;
        }
    }

    private static void VisitGroup(FilterGroup group, string path, bool isRoot, List<FilterError> errors)
    {
        if (group.Children is null || group.Children.Count == 0)
        {
            if (!isRoot)
            {
                errors.Add(new FilterError($"{path}.children", "group is empty"));
            }

            return;
        }

        for (var i = 0; i < group.Children.Count; i++)
        {
            Visit(group.Children[i], $"{path}.children[{i}]", isRoot: false, errors);
        }
    }

    private static void VisitCondition(FilterCondition condition, string path, List<FilterError> errors)
    {
        if (!Enum.IsDefined(condition.Field))
        {
            errors.Add(new FilterError($"{path}.field", $"unknown field; expected one of {FilterJson.Names<FilterField>()}"));
        }

        if (!Enum.IsDefined(condition.Operator))
        {
            errors.Add(new FilterError($"{path}.operator", $"unknown operator; expected one of {FilterJson.Names<FilterOperator>()}"));
        }

        if (condition.Domain is not null && condition.Field != FilterField.Category)
        {
            errors.Add(new FilterError($"{path}.domain", "domain only applies to the category field"));
        }

        if (condition.Value is null)
        {
            errors.Add(new FilterError($"{path}.value", "value is required"));
            return;
        }

        if (condition.Value.Length == 0 && condition.Operator != FilterOperator.Equal)
        {
            errors.Add(new FilterError($"{path}.value", "value must not be empty"));
            return;
        }

        if (condition.Operator == FilterOperator.Regex)
        {
            try
            {
                _ = RouteFilter.CreateRegex(condition);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new FilterError($"{path}.value", $"invalid regular expression: {ex.Message}"));
            }
        }
    }
}
