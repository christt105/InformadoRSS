using InformadoRSS.Scheduling;

namespace InformadoRSS.Tests.Scheduling;

public sealed class FetchBackoffTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 60)]
    [InlineData(5, 60)]
    [InlineData(1000, 60)]
    public void DoublesFromTheIntervalUpToTheCap(int failures, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), FetchBackoff.NextDelay(Interval, failures, Hour, null, Day));
    }

    [Fact]
    public void YouTubeOutageRecoversWithinTheCap()
    {
        var elapsed = TimeSpan.Zero;
        var failures = 0;
        while (elapsed < TimeSpan.FromHours(3))
        {
            failures++;
            elapsed += FetchBackoff.NextDelay(Interval, failures, Hour, null, Day);
        }

        Assert.True(elapsed - TimeSpan.FromHours(3) < Hour);
        Assert.Equal(Hour, FetchBackoff.NextDelay(Interval, failures, Hour, null, Day));
    }

    [Fact]
    public void IntervalLongerThanTheCapIsKept()
    {
        var interval = TimeSpan.FromHours(6);

        Assert.Equal(interval, FetchBackoff.NextDelay(interval, 5, Hour, null, Day));
    }

    [Fact]
    public void RetryAfterIsHonouredWhenLongerAndLimited()
    {
        Assert.Equal(TimeSpan.FromHours(3), FetchBackoff.NextDelay(Interval, 1, Hour, TimeSpan.FromHours(3), Day));
        Assert.Equal(Day, FetchBackoff.NextDelay(Interval, 1, Hour, TimeSpan.FromDays(30), Day));
        Assert.Equal(Interval, FetchBackoff.NextDelay(Interval, 1, Hour, TimeSpan.FromSeconds(5), Day));
    }
}
