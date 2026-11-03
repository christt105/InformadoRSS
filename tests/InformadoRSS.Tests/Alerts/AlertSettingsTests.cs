using InformadoRSS.Alerts;

namespace InformadoRSS.Tests.Alerts;

public sealed class AlertSettingsTests
{
    [Fact]
    public void DefaultsToDiscordWithoutTargetAndSixHours()
    {
        var settings = AlertSettings.FromValues(new Dictionary<string, string>());

        Assert.Equal(AlertTarget.Discord, settings.Target);
        Assert.Null(settings.DiscordChannelId);
        Assert.Null(settings.DiscordUserId);
        Assert.Null(settings.NtfyTopic);
        Assert.Equal(TimeSpan.FromHours(24), settings.FeedBrokenAfter);
        Assert.Empty(settings.Problems);
    }

    [Fact]
    public void ReadsEveryValue()
    {
        var settings = AlertSettings.FromValues(new Dictionary<string, string>
        {
            [AlertSettings.TargetKey] = " NTFY ",
            [AlertSettings.DiscordChannelIdKey] = "123",
            [AlertSettings.DiscordUserIdKey] = "456",
            [AlertSettings.NtfyTopicKey] = "servidor",
            [AlertSettings.FeedBrokenAfterMinutesKey] = "90",
        });

        Assert.Equal(AlertTarget.Ntfy, settings.Target);
        Assert.Equal("123", settings.DiscordChannelId);
        Assert.Equal("456", settings.DiscordUserId);
        Assert.Equal("servidor", settings.NtfyTopic);
        Assert.Equal(TimeSpan.FromMinutes(90), settings.FeedBrokenAfter);
        Assert.Empty(settings.Problems);
    }

    [Fact]
    public void InvalidValuesFallBackAndAreReported()
    {
        var settings = AlertSettings.FromValues(new Dictionary<string, string>
        {
            [AlertSettings.TargetKey] = "telegram",
            [AlertSettings.DiscordChannelIdKey] = "#alerts",
            [AlertSettings.DiscordUserIdKey] = "christian",
            [AlertSettings.NtfyTopicKey] = "a/b",
            [AlertSettings.FeedBrokenAfterMinutesKey] = "0",
        });

        Assert.Equal(AlertTarget.Discord, settings.Target);
        Assert.Null(settings.DiscordChannelId);
        Assert.Null(settings.DiscordUserId);
        Assert.Null(settings.NtfyTopic);
        Assert.Equal(AlertSettings.DefaultFeedBrokenAfter, settings.FeedBrokenAfter);
        Assert.Equal(5, settings.Problems.Count);
    }

    [Theory]
    [InlineData(30, "less than a minute")]
    [InlineData(45 * 60, "45m")]
    [InlineData(6 * 3600, "6h")]
    [InlineData((6 * 3600) + (5 * 60), "6h 5m")]
    [InlineData((2 * 86400) + (3 * 3600), "2d 3h")]
    [InlineData(86400, "1d")]
    public void FormatsDurations(int seconds, string expected) =>
        Assert.Equal(expected, AlertMessages.FormatDuration(TimeSpan.FromSeconds(seconds)));
}
