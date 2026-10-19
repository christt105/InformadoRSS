using InformadoRSS.Backups;

namespace InformadoRSS.Tests.Backups;

public sealed class BackupSettingsTests
{
    [Fact]
    public void DisabledByDefaultWithDailyBackupsKeptForTwoWeeks()
    {
        var settings = BackupSettings.FromValues(new Dictionary<string, string>());

        Assert.Null(settings.Directory);
        Assert.Equal(TimeSpan.FromHours(24), settings.Interval);
        Assert.Equal(14, settings.Keep);
        Assert.Empty(settings.Problems);
    }

    [Fact]
    public void ReadsEveryValue()
    {
        var settings = BackupSettings.FromValues(new Dictionary<string, string>
        {
            [BackupSettings.DirectoryKey] = " /backups ",
            [BackupSettings.IntervalHoursKey] = "6",
            [BackupSettings.KeepKey] = "30",
        });

        Assert.Equal("/backups", settings.Directory);
        Assert.Equal(TimeSpan.FromHours(6), settings.Interval);
        Assert.Equal(30, settings.Keep);
        Assert.Empty(settings.Problems);
    }

    [Fact]
    public void InvalidNumbersFallBackAndAreReported()
    {
        var settings = BackupSettings.FromValues(new Dictionary<string, string>
        {
            [BackupSettings.IntervalHoursKey] = "0",
            [BackupSettings.KeepKey] = "many",
        });

        Assert.Equal(BackupSettings.DefaultInterval, settings.Interval);
        Assert.Equal(BackupSettings.DefaultKeep, settings.Keep);
        Assert.Equal(2, settings.Problems.Count);
    }
}
