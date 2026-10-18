using InformadoRSS.Core;

namespace InformadoRSS.Tests.Scheduling;

/// <summary>Renders an item as its id, so tests can assert on what was delivered.</summary>
public sealed class FakeRenderer : IMessageRenderer
{
    public RenderedMessage Render(string? templateJson, FeedItem item) => new(item.Id);
}
