using InformadoRSS.Scheduling;
using Microsoft.Extensions.Configuration;

namespace InformadoRSS.Tests.Scheduling;

public sealed class SchedulerOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void MissingSectionUsesDefaults()
    {
        Assert.Equal(new SchedulerOptions(), SchedulerOptions.FromConfiguration(Config()));
    }

    [Fact]
    public void ValuesAreRead()
    {
        var options = SchedulerOptions.FromConfiguration(Config(
            ("Scheduler:Enabled", "false"),
            ("Scheduler:MaxConcurrency", "2"),
            ("Scheduler:MaxItemsPerRoute", "10"),
            ("Scheduler:MaxBackoff", "00:30:00"),
            ("Scheduler:PollInterval", "00:00:05")));

        Assert.False(options.Enabled);
        Assert.Equal(2, options.MaxConcurrency);
        Assert.Equal(10, options.MaxItemsPerRoute);
        Assert.Equal(TimeSpan.FromMinutes(30), options.MaxBackoff);
        Assert.Equal(TimeSpan.FromSeconds(5), options.PollInterval);
    }

    [Theory]
    [InlineData("Scheduler:Enabled", "maybe")]
    [InlineData("Scheduler:MaxConcurrency", "0")]
    [InlineData("Scheduler:MaxBackoff", "soon")]
    public void InvalidValuesAreRejected(string key, string value)
    {
        Assert.Throws<InvalidOperationException>(() => SchedulerOptions.FromConfiguration(Config((key, value))));
    }
}
