namespace InformadoRSS.Tests.Scheduling;

/// <summary>A clock that only moves when told to.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_lock)
        {
            _now += by;
        }
    }

    public void SetUtcNow(DateTimeOffset now)
    {
        lock (_lock)
        {
            _now = now;
        }
    }
}
