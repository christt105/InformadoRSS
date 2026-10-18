using InformadoRSS.Filters;

namespace InformadoRSS.Tests.Filters;

public sealed class FilterJsonTests
{
    [Fact]
    public void SerializesReadableShapeWithoutDefaults()
    {
        var tree = new AllGroup
        {
            Children =
            [
                new FilterCondition { Field = FilterField.Title, Operator = FilterOperator.Contains, Value = "Early access", Negate = true },
                new FilterCondition { Field = FilterField.Category, Domain = "type", Operator = FilterOperator.Equal, Value = "game" },
                new FilterCondition { Field = FilterField.Link, Operator = FilterOperator.StartsWith, Value = "https://", CaseSensitive = true },
            ],
        };

        var json = FilterJson.Serialize(tree);

        Assert.Equal(
            """{"type":"all","children":[{"type":"condition","field":"title","operator":"contains","value":"Early access","negate":true},{"type":"condition","field":"category","domain":"type","operator":"equals","value":"game"},{"type":"condition","field":"link","operator":"startsWith","value":"https://","caseSensitive":true}]}""",
            json);
    }

    [Fact]
    public void RoundTripPreservesTree()
    {
        const string json = """{"type":"any","children":[{"type":"condition","field":"description","operator":"regex","value":"^Completed","caseSensitive":true},{"type":"all","children":[{"type":"condition","field":"author","operator":"equals","value":""},{"type":"condition","field":"category","domain":"status","operator":"contains","value":"fin"}]}],"negate":true}""";

        var result = FilterJson.Parse(json);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var root = Assert.IsType<AnyGroup>(result.Root);
        Assert.True(root.Negate);
        var regex = Assert.IsType<FilterCondition>(root.Children[0]);
        Assert.Equal(FilterOperator.Regex, regex.Operator);
        Assert.True(regex.CaseSensitive);
        Assert.IsType<AllGroup>(root.Children[1]);
        Assert.Equal(json, FilterJson.Serialize(result.Root));
    }

    [Fact]
    public void AbsentFilterSerializesAsNull()
    {
        Assert.Equal("null", FilterJson.Serialize(null));
        Assert.Null(FilterJson.Parse("null").Root);
    }

    [Fact]
    public void AcceptsDiscriminatorAfterOtherProperties()
    {
        var result = FilterJson.Parse("""{"field": "title", "operator": "equals", "value": "x", "type": "condition"}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsType<FilterCondition>(result.Root);
    }

    [Theory]
    [InlineData("""{"type": "condition", "field": "titel", "operator": "contains", "value": "x"}""", "$.field", "unknown field; expected one of \"title\", \"description\", \"author\", \"link\", \"category\"")]
    [InlineData("""{"type": "condition", "field": "title", "operator": "like", "value": "x"}""", "$.operator", "unknown operator")]
    [InlineData("""{"type": "or", "children": []}""", "$", "unknown node type")]
    [InlineData("""{"children": []}""", "$", "\"type\"")]
    [InlineData("""{"type": "condition", "field": "title", "value": "x"}""", "$", "\"field\", \"operator\" and \"value\"")]
    [InlineData("""{"type": "condition", "field": "title", "operator": "contains", "value": "x", "negated": true}""", "$", "unknown property")]
    [InlineData("""{"type": "all", "children": [""", "$", "invalid JSON")]
    public void ReportsUnreadableJson(string json, string path, string message)
    {
        var result = FilterJson.Parse(json);

        Assert.False(result.IsValid);
        Assert.Null(result.Root);
        var error = Assert.Single(result.Errors);
        Assert.StartsWith(path, error.Path, StringComparison.Ordinal);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsEverySemanticError()
    {
        var result = FilterJson.Parse("""
            {
              "type": "all",
              "children": [
                {"type": "condition", "field": "title", "operator": "regex", "value": "(unclosed"},
                {"type": "any", "children": []},
                {"type": "condition", "field": "author", "domain": "type", "operator": "equals", "value": "x"},
                {"type": "condition", "field": "link", "operator": "contains", "value": ""},
                {"type": "condition", "field": "title", "operator": "contains", "value": null},
                null
              ]
            }
            """);

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Errors,
            error =>
            {
                Assert.Equal("$.children[0].value", error.Path);
                Assert.StartsWith("invalid regular expression", error.Message, StringComparison.Ordinal);
            },
            error => Assert.Equal("$.children[1].children: group is empty", error.ToString()),
            error => Assert.Equal("$.children[2].domain: domain only applies to the category field", error.ToString()),
            error => Assert.Equal("$.children[3].value: value must not be empty", error.ToString()),
            error => Assert.Equal("$.children[4].value: value is required", error.ToString()),
            error => Assert.Equal("$.children[5]: node is missing", error.ToString()));
    }

    [Fact]
    public void RejectsNumericEnumValues()
    {
        var result = FilterJson.Parse("""{"type": "condition", "field": 42, "operator": "contains", "value": "x"}""");

        Assert.False(result.IsValid);
        Assert.Equal("$.field", Assert.Single(result.Errors).Path);
    }
}
