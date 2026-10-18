using System.Text.Json;

namespace InformadoRSS.Filters;

/// <summary>Reads and writes the JSON form of a filter tree, as stored in the database and edited by the web UI.</summary>
public static class FilterJson
{
    /// <summary>Serializes a filter tree; an absent filter becomes <c>null</c>.</summary>
    public static string Serialize(FilterNode? root) =>
        root is null ? "null" : JsonSerializer.Serialize(root, FilterJsonContext.Default.FilterNode);

    /// <summary>
    /// Parses and validates a filter. A <see langword="null"/>, blank or <c>null</c> JSON input is a valid absent filter.
    /// Never throws for bad input: problems are reported in <see cref="FilterParseResult.Errors"/>.
    /// </summary>
    public static FilterParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FilterParseResult(null, []);
        }

        FilterNode? root;
        try
        {
            root = JsonSerializer.Deserialize(json, FilterJsonContext.Default.FilterNode);
        }
        catch (JsonException ex)
        {
            return Failed(ex.Path ?? "$", DescribeJsonError(ex));
        }
        catch (NotSupportedException)
        {
            return Failed("$", "every node needs a \"type\": \"all\", \"any\" or \"condition\"");
        }

        return new FilterParseResult(root, FilterValidator.Validate(root));
    }

    private static FilterParseResult Failed(string path, string message) =>
        new(null, [new FilterError(path, message)]);

    private static string DescribeJsonError(JsonException ex)
    {
        var path = ex.Path ?? string.Empty;
        var message = ex.Message;
        if (message.Contains("type discriminator", StringComparison.OrdinalIgnoreCase))
        {
            return "unknown node type; expected \"all\", \"any\" or \"condition\"";
        }

        if (message.Contains("missing required properties", StringComparison.OrdinalIgnoreCase))
        {
            return "a condition needs \"field\", \"operator\" and \"value\"";
        }

        if (message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase))
        {
            return "unknown property";
        }

        if (path.EndsWith(".field", StringComparison.Ordinal))
        {
            return $"unknown field; expected one of {Names<FilterField>()}";
        }

        if (path.EndsWith(".operator", StringComparison.Ordinal))
        {
            return $"unknown operator; expected one of {Names<FilterOperator>()}";
        }

        return ex.InnerException is null ? "invalid JSON" : $"invalid JSON: {ex.InnerException.Message}";
    }

    internal static string Names<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetValues<TEnum>().Select(value => JsonSerializer.Serialize(value, typeof(TEnum), FilterJsonContext.Default)));
}
