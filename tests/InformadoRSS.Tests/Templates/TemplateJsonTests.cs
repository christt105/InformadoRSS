using InformadoRSS.Templates;

namespace InformadoRSS.Tests.Templates;

public sealed class TemplateJsonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("null")]
    public void AbsentTemplateIsValid(string? json)
    {
        var result = TemplateJson.Parse(json);

        Assert.True(result.IsValid);
        Assert.Null(result.Template);
    }

    [Fact]
    public void RoundTripsAFullTemplate()
    {
        var template = new MessageTemplate(
            "{{title}}",
            new EmbedTemplate("{{title}}", "{{description}}", "{{link}}", 0, "{{image}}", "{{image}}", "{{category}}", "{{author}}", true));

        var json = TemplateJson.Serialize(template);
        var result = TemplateJson.Parse(json);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(template, result.Template);
        Assert.Contains("\"color\":0", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeOmitsEmptyFields()
    {
        Assert.Equal("""{"content":"**{{title}}**\n{{link}}"}""", TemplateJson.Serialize(MessageTemplate.Default));
        Assert.Equal("null", TemplateJson.Serialize(null));
    }

    [Theory]
    [InlineData("""{"content":"x","colour":1}""", "$.colour", "unknown property")]
    [InlineData("""{"embed":{"title":"x","color":"red"}}""", "$.embed.color", "must be a number")]
    [InlineData("""{"embed":{"title":"x","timestamp":"yes"}}""", "$.embed.timestamp", "true or false")]
    [InlineData("""{"content":""", "$.content", "invalid JSON")]
    [InlineData("""{}""", "$", "needs \"content\", an \"embed\"")]
    [InlineData("""{"embed":{"url":"{{link}}","color":1}}""", "$.embed", "at least a title")]
    [InlineData("""{"embed":{"title":"x","color":16777216}}""", "$.embed.color", "between 0 and 16777215")]
    [InlineData("""{"embed":{"title":"x","color":-1}}""", "$.embed.color", "between 0 and 16777215")]
    [InlineData("""{"embed":{"title":"x","image":"cover.png"}}""", "$.embed.image", "http(s) URL")]
    [InlineData("""{"content":"{{feed}} {{titel}}"}""", "$.content", "unknown placeholder {{feed}}")]
    [InlineData("""{"content":"{{category:}}"}""", "$.content", "unknown placeholder {{category:}}")]
    public void ReportsReadableErrors(string json, string path, string message)
    {
        var result = TemplateJson.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Path == path && error.Message.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsEveryUnknownPlaceholderOnce()
    {
        var result = TemplateJson.Parse("""{"content":"{{a}} {{b}} {{a}}","embed":{"footer":"{{c}}"}}""");

        Assert.Equal(
            ["$.content: unknown placeholder {{a}}", "$.content: unknown placeholder {{b}}", "$.embed.footer: unknown placeholder {{c}}"],
            result.Errors.Select(error => error.ToString()[..error.ToString().IndexOf(';', StringComparison.Ordinal)]));
    }

    [Fact]
    public void AcceptsLiteralUrls()
    {
        var result = TemplateJson.Parse("""{"embed":{"title":"x","thumbnail":"https://example.org/logo.png"}}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }
}
