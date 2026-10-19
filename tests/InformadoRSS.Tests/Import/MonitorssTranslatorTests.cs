using System.Text.Json;
using InformadoRSS.Filters;
using InformadoRSS.Import;

namespace InformadoRSS.Tests.Import;

public sealed class MonitorssTranslatorTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Relational(string op, string field, string value, string not = "null") =>
        $$"""{"type":"RELATIONAL","op":"{{op}}","left":{"type":"ARTICLE","value":"{{field}}"},"right":{"type":"STRING","value":"{{value}}"},"not":{{not}}}""";

    private static string And(params string[] children) => $$"""{"type":"LOGICAL","op":"AND","children":[{{string.Join(",", children)}}]}""";

    private static string Or(params string[] children) => $$"""{"type":"LOGICAL","op":"OR","children":[{{string.Join(",", children)}}]}""";

    [Fact]
    public void TranslatesDescriptionContains()
    {
        var result = MonitorssFilterTranslator.Translate(Json(And(Relational("CONTAINS", "description", "Completed"))));

        Assert.True(result.IsTranslated);
        Assert.Equal(
            """{"type":"all","children":[{"type":"condition","field":"description","operator":"contains","value":"Completed"}]}""",
            FilterJson.Serialize(result.Root));
    }

    [Fact]
    public void TranslatesNegatedTitleContains()
    {
        var result = MonitorssFilterTranslator.Translate(Json(And(Relational("CONTAINS", "title", "Early access", "true"))));

        var condition = Assert.IsType<FilterCondition>(Assert.Single(Assert.IsType<AllGroup>(result.Root).Children));
        Assert.True(condition.Negate);
        Assert.Equal(FilterField.Title, condition.Field);
        Assert.Equal(FilterOperator.Contains, condition.Operator);
        Assert.False(condition.CaseSensitive);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void TranslatesEqualsAsCaseSensitiveAndRegexWithNote()
    {
        var result = MonitorssFilterTranslator.Translate(Json(Or(Relational("EQ", "author", "Ori"), Relational("MATCHES", "link", "^https://"))));

        var group = Assert.IsType<AnyGroup>(result.Root);
        var equal = Assert.IsType<FilterCondition>(group.Children[0]);
        Assert.Equal(FilterOperator.Equal, equal.Operator);
        Assert.True(equal.CaseSensitive);
        Assert.Equal(FilterOperator.Regex, Assert.IsType<FilterCondition>(group.Children[1]).Operator);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void EmptyGroupsMatchEverything()
    {
        Assert.Null(MonitorssFilterTranslator.Translate(Json(And())).Root);
        Assert.Null(MonitorssFilterTranslator.Translate(Json(Or(Relational("CONTAINS", "title", "x"), And()))).Root);

        var nested = MonitorssFilterTranslator.Translate(Json(And(Relational("CONTAINS", "title", "x"), Or())));
        Assert.True(nested.IsTranslated);
        Assert.IsType<FilterCondition>(Assert.Single(Assert.IsType<AllGroup>(nested.Root).Children));
    }

    [Theory]
    [InlineData("summary", "CONTAINS")]
    [InlineData("title", "STARTS_WITH")]
    public void ReportsUntranslatableParts(string field, string op)
    {
        var result = MonitorssFilterTranslator.Translate(Json(And(Relational("CONTAINS", "title", "ok"), Relational(op, field, "x"))));

        Assert.False(result.IsTranslated);
        Assert.Null(result.Root);
        Assert.Single(result.Problems);
    }

    [Fact]
    public void NoExpressionMeansNoFilter()
    {
        var result = MonitorssFilterTranslator.Translate(null);

        Assert.True(result.IsTranslated);
        Assert.Null(result.Root);
    }

    [Fact]
    public void KeepsSupportedPlaceholders()
    {
        var result = MonitorssTemplateTranslator.Translate("**{{title}}**\n{{link}}");

        Assert.Equal("**{{title}}**\n{{link}}", result.Template!.Content);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyContentUsesTheDefaultTemplate(string? content) =>
        Assert.Null(MonitorssTemplateTranslator.Translate(content).Template);

    [Fact]
    public void RewritesAndRemovesPlaceholders()
    {
        var result = MonitorssTemplateTranslator.Translate("{{ pubdate }} {{title}} {{summary}} {{custom::x}}");

        Assert.Equal("{{published}} {{title}}  ", result.Template!.Content);
        Assert.Contains("removed unsupported placeholder {{summary}}", result.Notes);
        Assert.Contains("removed unsupported placeholder {{custom::x}}", result.Notes);
        Assert.Equal(3, result.Notes.Count);
    }

    [Fact]
    public void ContentWithOnlyUnknownPlaceholdersFallsBackToDefault()
    {
        var result = MonitorssTemplateTranslator.Translate("{{summary}}");

        Assert.Null(result.Template);
        Assert.Equal(2, result.Notes.Count);
    }
}
