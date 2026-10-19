using InformadoRSS.Backups;

namespace InformadoRSS.Tests.Backups;

public sealed class BackupFilesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("informadorss-backup-files-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void Touch(string name) => File.WriteAllText(Path.Combine(_directory, name), string.Empty);

    [Fact]
    public void NamesFilesAfterTheUtcTime()
    {
        var name = BackupFiles.FileName(new DateTimeOffset(2026, 10, 19, 18, 5, 9, TimeSpan.FromHours(2)));

        Assert.Equal("informadorss-20261019T160509Z.db", name);
    }

    [Fact]
    public void ListsOnlyBackupsNewestFirst()
    {
        Touch("informadorss-20261018T000000Z.db");
        Touch("informadorss-20261019T000000Z.db");
        Touch("informadorss-20261019T120000Z.db.tmp");
        Touch("informadorss-manual.db");
        Touch("notes.txt");

        var files = BackupFiles.List(_directory);

        Assert.Equal(
            ["informadorss-20261019T000000Z.db", "informadorss-20261018T000000Z.db"],
            files.Select(file => Path.GetFileName(file.Path)));
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero), files[0].TakenAt);
    }

    [Fact]
    public void MissingFolderHasNoBackups() =>
        Assert.Empty(BackupFiles.List(Path.Combine(_directory, "missing")));

    [Fact]
    public void PruneKeepsTheNewestAndLeavesOtherFilesAlone()
    {
        for (var day = 10; day <= 15; day++)
        {
            Touch($"informadorss-202610{day}T000000Z.db");
        }

        Touch("notes.txt");

        Assert.Equal(4, BackupFiles.Prune(_directory, 2));
        Assert.Equal(
            ["informadorss-20261014T000000Z.db", "informadorss-20261015T000000Z.db", "notes.txt"],
            Directory.EnumerateFiles(_directory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }
}
