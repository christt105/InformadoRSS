using System.Collections.Concurrent;
using InformadoRSS.Core;

namespace InformadoRSS.Tests.Scheduling;

public sealed class FakeSource : ISource
{
    public static CacheValidators V1 { get; } = new("\"v1\"", null);

    public static CacheValidators V2 { get; } = new("\"v2\"", null);

    public Func<FeedDefinition, CancellationToken, Task<FetchResult>> Handler { get; set; } =
        (_, _) => Task.FromResult<FetchResult>(new FeedFetched(null, [], CacheValidators.None));

    public ConcurrentQueue<FeedDefinition> Requests { get; } = new();

    public void Returns(FetchResult result) => Handler = (_, _) => Task.FromResult(result);

    public void ReturnsItems(CacheValidators validators, params FeedItem[] items) => Returns(new FeedFetched("Feed", items, validators));

    public void ReturnsItems(params FeedItem[] items) => ReturnsItems(V1, items);

    public Task<FetchResult> FetchAsync(FeedDefinition feed, CancellationToken cancellationToken)
    {
        Requests.Enqueue(feed);
        return Handler(feed, cancellationToken);
    }
}
