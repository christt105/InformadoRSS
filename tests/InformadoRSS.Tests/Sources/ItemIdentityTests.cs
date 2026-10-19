using System.Text.RegularExpressions;
using InformadoRSS.Core;
using InformadoRSS.Sources;

namespace InformadoRSS.Tests.Sources;

public sealed class ItemIdentityTests
{
    private const string MyAnimeListStatus = "^[^-]+";

    private static FeedItem Item(string id, string? description) =>
        new(id, id, $"https://example.com/{id}", null, description, null, null, [], null);

    [Fact]
    public void WithoutPatternItemsAreReturnedUnchanged()
    {
        IReadOnlyList<FeedItem> items = [Item("a", "Completed - 12 of 12 episodes")];

        Assert.Same(items, ItemIdentity.Apply(items, null));
        Assert.Same(items, ItemIdentity.Apply(items, ""));
    }

    [Fact]
    public void MatchIsTrimmedAndAppendedToTheId()
    {
        var items = ItemIdentity.Apply(
            [Item("a", "Watching - 3 of 12 episodes"), Item("b", "Plan to Watch - 0 of ? episodes")], MyAnimeListStatus);

        Assert.Equal(["a#Watching", "b#Plan to Watch"], items.Select(item => item.Id));
    }

    [Fact]
    public void ProgressInsideTheSameStatusKeepsTheId()
    {
        var first = ItemIdentity.Apply([Item("a", "Watching - 1 of 12 episodes")], MyAnimeListStatus);
        var later = ItemIdentity.Apply([Item("a", "Watching - 7 of 12 episodes")], MyAnimeListStatus);

        Assert.Equal(first[0].Id, later[0].Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   - no status")]
    public void ItemsWithoutAMatchKeepTheirId(string? description)
    {
        var items = ItemIdentity.Apply([Item("a", description)], MyAnimeListStatus);

        Assert.Equal("a", items[0].Id);
    }

    [Fact]
    public void InvalidPatternThrows()
    {
        Assert.Throws<RegexParseException>(() => ItemIdentity.CreateRegex("("));
    }
}
