using System.Text.Json;
using InformadoRSS.Filters;

namespace InformadoRSS.Import;

/// <summary>
/// Translates a MonitoRSS filter expression (<c>LOGICAL</c> AND/OR nodes with <c>children</c>, and <c>RELATIONAL</c>
/// EQ/CONTAINS/MATCHES nodes comparing an article field on the <c>left</c> with a string on the <c>right</c>, optionally
/// negated with <c>not</c>) into a route filter with the same meaning.
/// </summary>
public static class MonitorssFilterTranslator
{
    private static readonly Dictionary<string, FilterField> Fields = new(StringComparer.Ordinal)
    {
        ["title"] = FilterField.Title,
        ["description"] = FilterField.Description,
        ["author"] = FilterField.Author,
        ["link"] = FilterField.Link,
        ["categories"] = FilterField.Category,
    };

    public static FilterTranslation Translate(JsonElement? expression)
    {
        if (expression is not { ValueKind: JsonValueKind.Object } root)
        {
            return new FilterTranslation(null, [], []);
        }

        var problems = new List<string>();
        var notes = new List<string>();
        var node = Visit(root, problems, notes);
        if (problems.Count > 0)
        {
            return new FilterTranslation(null, problems, notes);
        }

        var errors = FilterValidator.Validate(node.Node);
        return errors.Count > 0
            ? new FilterTranslation(null, [.. errors.Select(error => $"translated filter is invalid: {error}")], notes)
            : new FilterTranslation(node.Node, [], notes);
    }

    private static Translated Visit(JsonElement element, List<string> problems, List<string> notes)
    {
        var type = Text(element, "type");
        return type switch
        {
            "LOGICAL" => VisitLogical(element, problems, notes),
            "RELATIONAL" => VisitRelational(element, problems, notes),
            _ => Fail(problems, $"unsupported expression type \"{type}\""),
        };
    }

    private static Translated VisitLogical(JsonElement element, List<string> problems, List<string> notes)
    {
        var op = Text(element, "op");
        if (op is not ("AND" or "OR"))
        {
            return Fail(problems, $"unsupported logical operator \"{op}\"");
        }

        var children = new List<FilterNode>();
        var anyAlwaysTrue = false;
        if (element.TryGetProperty("children", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in list.EnumerateArray())
            {
                var translated = Visit(child, problems, notes);
                if (translated.Node is null)
                {
                    anyAlwaysTrue |= translated.AlwaysTrue;
                }
                else
                {
                    children.Add(translated.Node);
                }
            }
        }

        if (problems.Count > 0)
        {
            return default;
        }

        FilterGroup? group = op == "AND"
            ? children.Count == 0 ? null : new AllGroup { Children = children }
            : anyAlwaysTrue || children.Count == 0 ? null : new AnyGroup { Children = children };
        return group is null ? Translated.True : new Translated(group, false);
    }

    private static Translated VisitRelational(JsonElement element, List<string> problems, List<string> notes)
    {
        var op = Text(element, "op");
        var negate = element.TryGetProperty("not", out var not) && not.ValueKind == JsonValueKind.True;
        var left = element.TryGetProperty("left", out var l) ? l : default;
        var right = element.TryGetProperty("right", out var r) ? r : default;
        var leftType = left.ValueKind == JsonValueKind.Object ? Text(left, "type") : null;
        var fieldName = left.ValueKind == JsonValueKind.Object ? Text(left, "value") : null;
        var rightType = right.ValueKind == JsonValueKind.Object ? Text(right, "type") : null;
        var value = right.ValueKind == JsonValueKind.Object && right.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

        if (leftType != "ARTICLE")
        {
            return Fail(problems, $"unsupported comparison source \"{leftType}\"");
        }

        if (fieldName is null || !Fields.TryGetValue(fieldName, out var field))
        {
            return Fail(problems, $"unsupported article field \"{fieldName}\" (supported: {string.Join(", ", Fields.Keys)})");
        }

        if (rightType != "STRING" || value is null)
        {
            return Fail(problems, $"unsupported comparison value of type \"{rightType}\" for field \"{fieldName}\"");
        }

        FilterCondition condition;
        switch (op)
        {
            case "CONTAINS" when value.Length == 0:
                return negate ? Fail(problems, $"\"{fieldName}\" NOT CONTAINS an empty string never matches") : Translated.True;
            case "CONTAINS":
                condition = new FilterCondition { Field = field, Operator = FilterOperator.Contains, Value = value, Negate = negate };
                break;
            case "EQ":
                condition = new FilterCondition
                {
                    Field = field, Operator = FilterOperator.Equal, Value = value, CaseSensitive = true, Negate = negate,
                };
                break;
            case "MATCHES":
                condition = new FilterCondition { Field = field, Operator = FilterOperator.Regex, Value = value, Negate = negate };
                notes.Add($"regular expression on \"{fieldName}\" was written for JavaScript and now runs as a .NET regex: {value}");
                break;
            default:
                return Fail(problems, $"unsupported relational operator \"{op}\"");
        }

        if (field == FilterField.Category)
        {
            notes.Add("categories are compared one by one instead of as a single joined text");
        }

        return new Translated(condition, false);
    }

    private static Translated Fail(List<string> problems, string message)
    {
        problems.Add(message);
        return default;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <param name="Node">The translated node, or <c>null</c> when the node always matches or failed to translate.</param>
    private readonly record struct Translated(FilterNode? Node, bool AlwaysTrue)
    {
        public static Translated True => new(null, true);
    }
}
