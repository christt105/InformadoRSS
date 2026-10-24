using InformadoRSS.Core;

namespace InformadoRSS.Tests.Scheduling;

/// <summary>Renders an item as its id, so tests can assert on what was delivered.</summary>
public sealed class FakeRenderer : IMessageRenderer
{
    /// <summary>Items whose rendering throws, as a broken template would.</summary>
    public HashSet<string> Failing { get; } = new(StringComparer.Ordinal);

    public RenderedMessage Render(string? templateJson, FeedItem item) =>
        Failing.Contains(item.Id) ? throw new FormatException($"Cannot render {item.Id}.") : new(item.Id);
}
