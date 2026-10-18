using InformadoRSS.Core;

namespace InformadoRSS.Tests.Scheduling;

/// <summary>Records every delivery attempt; <see cref="Handler"/> decides the result.</summary>
public sealed class FakeSink(string name) : ISink
{
    private readonly Lock _lock = new();
    private readonly List<(Destination Destination, string Content)> _attempts = [];

    public string Name { get; } = name;

    public Func<Destination, RenderedMessage, DeliveryResult> Handler { get; set; } = (_, _) => DeliveryResult.Delivered;

    public IReadOnlyList<(Destination Destination, string Content)> Attempts
    {
        get
        {
            lock (_lock)
            {
                return [.. _attempts];
            }
        }
    }

    public IReadOnlyList<string> Contents => [.. Attempts.Select(attempt => attempt.Content)];

    public Task<DeliveryResult> DeliverAsync(Destination destination, RenderedMessage message, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _attempts.Add((destination, message.Content));
        }

        return Task.FromResult(Handler(destination, message));
    }
}
