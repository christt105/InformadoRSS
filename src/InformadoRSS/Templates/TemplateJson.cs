using System.Text.Json;

namespace InformadoRSS.Templates;

/// <summary>Reads and writes the JSON form of a route template, as stored in the database and edited by the web UI.</summary>
public static class TemplateJson
{
    /// <summary>Serializes a template; an absent template (the default) becomes <c>null</c>.</summary>
    public static string Serialize(MessageTemplate? template) =>
        template is null ? "null" : JsonSerializer.Serialize(template, TemplateJsonContext.Default.MessageTemplate);

    /// <summary>
    /// Parses and validates a template. A <see langword="null"/>, blank or <c>null</c> JSON input is a valid absent
    /// template. Never throws for bad input: problems are reported in <see cref="TemplateParseResult.Errors"/>.
    /// </summary>
    public static TemplateParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TemplateParseResult(null, []);
        }

        MessageTemplate? template;
        try
        {
            template = JsonSerializer.Deserialize(json, TemplateJsonContext.Default.MessageTemplate);
        }
        catch (JsonException ex)
        {
            return new TemplateParseResult(null, [new TemplateError(ex.Path ?? "$", DescribeJsonError(ex))]);
        }

        return new TemplateParseResult(template, template is null ? [] : TemplateValidator.Validate(template));
    }

    private static string DescribeJsonError(JsonException ex)
    {
        if (ex.Message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase))
        {
            return "unknown property; a template has \"content\" and \"embed\", and an embed has \"title\", "
                + "\"description\", \"url\", \"color\", \"image\", \"thumbnail\", \"footer\", \"author\" and \"timestamp\"";
        }

        var path = ex.Path ?? string.Empty;
        if (path.EndsWith(".color", StringComparison.Ordinal))
        {
            return "the color must be a number, e.g. 5793266 for 0x5865F2";
        }

        if (path.EndsWith(".timestamp", StringComparison.Ordinal))
        {
            return "timestamp must be true or false";
        }

        return ex.InnerException is null ? "invalid JSON or a value of the wrong type" : $"invalid JSON: {ex.InnerException.Message}";
    }
}
