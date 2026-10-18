using System.Text.RegularExpressions;
using InformadoRSS.Core;
using InformadoRSS.Filters;

namespace InformadoRSS.Tests.Filters;

public sealed class RouteFilterTests
{
    private static FeedItem Item(
        string? title = null,
        string? description = null,
        string? author = null,
        string? link = null,
        params FeedCategory[] categories) =>
        new("id", title, link, description, description, author, null, categories, null);

    private static RouteFilter FromJson(string json)
    {
        var result = FilterJson.Parse(json);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        return RouteFilter.Compile(result.Root);
    }

    [Fact]
    public void MyAnimeListCompletedFilter()
    {
        var filter = FromJson("""
            {"type": "condition", "field": "description", "operator": "contains", "value": "Completed"}
            """);

        Assert.True(filter.Matches(Item(title: "Frieren", description: "Completed - 28 of 28 episodes")));
        Assert.True(filter.Matches(Item(title: "Frieren", description: "completed - 28 of 28 episodes")));
        Assert.False(filter.Matches(Item(title: "Frieren", description: "Watching - 3 of 28 episodes")));
        Assert.False(filter.Matches(Item(title: "Frieren")));
    }

    [Fact]
    public void ObsidianChangelogSkipsEarlyAccess()
    {
        var filter = FromJson("""
            {"type": "condition", "field": "title", "operator": "contains", "value": "Early access", "negate": true}
            """);

        Assert.True(filter.Matches(Item(title: "Obsidian 1.9.10 Desktop")));
        Assert.False(filter.Matches(Item(title: "Obsidian 1.10.0 Desktop (Early access)")));
        Assert.False(filter.Matches(Item(title: "EARLY ACCESS: Obsidian Mobile 1.10")));
        Assert.True(filter.Matches(Item()));
    }

    [Fact]
    public void MediaTrackerRoutesMoviesAndShowsSeparatelyFromGames()
    {
        var watched = FromJson("""
            {
              "type": "any",
              "children": [
                {"type": "condition", "field": "category", "domain": "type", "operator": "equals", "value": "movie"},
                {"type": "condition", "field": "category", "domain": "type", "operator": "equals", "value": "tv"}
              ]
            }
            """);
        var played = FromJson("""
            {"type": "condition", "field": "category", "domain": "type", "operator": "equals", "value": "game"}
            """);

        var movie = Item(title: "Perfect Days", categories: [new("type", "movie"), new("status", "finished")]);
        var show = Item(title: "Severance", categories: [new("type", "tv"), new("status", "finished")]);
        var game = Item(title: "Hollow Knight", categories: [new("type", "game"), new("status", "finished")]);
        var untyped = Item(title: "Movie night", categories: [new("status", "movie")]);

        Assert.True(watched.Matches(movie));
        Assert.True(watched.Matches(show));
        Assert.False(watched.Matches(game));
        Assert.False(watched.Matches(untyped));

        Assert.False(played.Matches(movie));
        Assert.False(played.Matches(show));
        Assert.True(played.Matches(game));
    }

    [Fact]
    public void CategoryRegexMatchesAnyCategory()
    {
        var filter = FromJson("""
            {"type": "condition", "field": "category", "domain": "TYPE", "operator": "regex", "value": "^(movie|tv)$"}
            """);

        Assert.True(filter.Matches(Item(categories: [new("status", "finished"), new("type", "TV")])));
        Assert.False(filter.Matches(Item(categories: [new("type", "tv special")])));
        Assert.False(filter.Matches(Item(categories: [new(null, "movie")])));
    }

    [Fact]
    public void CategoryWithoutDomainMatchesAnyDomain()
    {
        var filter = FromJson("""
            {"type": "condition", "field": "category", "operator": "equals", "value": "movie"}
            """);

        Assert.True(filter.Matches(Item(categories: [new(null, "movie")])));
        Assert.True(filter.Matches(Item(categories: [new("type", "movie")])));
        Assert.False(filter.Matches(Item()));
    }

    [Fact]
    public void NestedGroupsCombineAndOr()
    {
        var filter = FromJson("""
            {
              "type": "all",
              "children": [
                {"type": "condition", "field": "link", "operator": "startsWith", "value": "https://youtube.com/"},
                {
                  "type": "any",
                  "children": [
                    {"type": "condition", "field": "author", "operator": "equals", "value": "Christian"},
                    {"type": "condition", "field": "title", "operator": "regex", "value": "\\bgodot\\b"}
                  ]
                },
                {
                  "type": "any",
                  "negate": true,
                  "children": [
                    {"type": "condition", "field": "title", "operator": "contains", "value": "#shorts"},
                    {"type": "condition", "field": "link", "operator": "contains", "value": "/shorts/"}
                  ]
                }
              ]
            }
            """);

        Assert.True(filter.Matches(Item(title: "Devlog 3", author: "christian", link: "https://youtube.com/watch?v=1")));
        Assert.True(filter.Matches(Item(title: "Godot tips", author: "Someone", link: "https://youtube.com/watch?v=2")));
        Assert.False(filter.Matches(Item(title: "Godotesque", author: "Someone", link: "https://youtube.com/watch?v=3")));
        Assert.False(filter.Matches(Item(title: "Devlog 3", author: "Christian", link: "https://vimeo.com/1")));
        Assert.False(filter.Matches(Item(title: "Godot #shorts", author: "Christian", link: "https://youtube.com/watch?v=4")));
        Assert.False(filter.Matches(Item(title: "Godot", author: "Christian", link: "https://youtube.com/shorts/5")));
    }

    [Fact]
    public void NegatedConditionMatchesWhenFieldIsMissing()
    {
        var filter = RouteFilter.Compile(new FilterCondition
        {
            Field = FilterField.Author,
            Operator = FilterOperator.Equal,
            Value = "bot",
            Negate = true,
        });

        Assert.True(filter.Matches(Item()));
        Assert.True(filter.Matches(Item(author: "human")));
        Assert.False(filter.Matches(Item(author: "BOT")));
    }

    [Fact]
    public void CaseSensitiveComparisonsRespectCasing()
    {
        var contains = RouteFilter.Compile(new FilterCondition
        {
            Field = FilterField.Title,
            Operator = FilterOperator.Contains,
            Value = "Completed",
            CaseSensitive = true,
        });
        var regex = RouteFilter.Compile(new FilterCondition
        {
            Field = FilterField.Title,
            Operator = FilterOperator.Regex,
            Value = "^Done",
            CaseSensitive = true,
        });

        Assert.True(contains.Matches(Item(title: "Completed")));
        Assert.False(contains.Matches(Item(title: "completed")));
        Assert.True(regex.Matches(Item(title: "Done!")));
        Assert.False(regex.Matches(Item(title: "done!")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("""{"type": "all", "children": []}""")]
    [InlineData("""{"type": "any"}""")]
    public void AbsentOrEmptyFilterPassesEverything(string? json)
    {
        var result = FilterJson.Parse(json);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var filter = RouteFilter.Compile(result.Root);
        Assert.Same(RouteFilter.PassAll, filter);
        Assert.True(filter.Matches(Item()));
    }

    [Fact]
    public void RegexTimesOutInsteadOfHanging()
    {
        var filter = RouteFilter.Compile(new FilterCondition
        {
            Field = FilterField.Title,
            Operator = FilterOperator.Regex,
            Value = "^(a+)+$",
        });

        Assert.Throws<RegexMatchTimeoutException>(() => filter.Matches(Item(title: new string('a', 64) + "!")));
    }

    [Fact]
    public void CompileRejectsInvalidTree()
    {
        var invalid = new AllGroup { Children = [new AnyGroup()] };

        var ex = Assert.Throws<ArgumentException>(() => RouteFilter.Compile(invalid));
        Assert.Contains("$.children[0].children: group is empty", ex.Message, StringComparison.Ordinal);
    }
}
