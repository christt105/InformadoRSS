using InformadoRSS.Core;
using InformadoRSS.Scheduling;

namespace InformadoRSS.Tests.Scheduling;

public sealed class PlainTextMessageRendererTests
{
    private readonly PlainTextMessageRenderer _renderer = new();

    [Fact]
    public void RendersBoldTitleAndLink()
    {
        var message = _renderer.Render(null, PipelineHarness.Item("a", title: " Hello "));

        Assert.Equal("**Hello**\nhttps://example.com/a", message.Content);
    }

    [Fact]
    public void FallsBackToTheIdWhenThereIsNothingElse()
    {
        var item = new FeedItem("urn:1", null, null, null, null, null, null, [], null);

        Assert.Equal("urn:1", _renderer.Render(null, item).Content);
    }
}
