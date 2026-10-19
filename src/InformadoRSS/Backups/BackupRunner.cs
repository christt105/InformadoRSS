using InformadoRSS.Alerts;
using InformadoRSS.Storage;
using Microsoft.Data.Sqlite;

namespace InformadoRSS.Backups;

/// <summary>
/// Writes a copy of the database to the configured folder once the newest backup there is older than the interval,
/// then deletes the oldest copies beyond the configured count. The copy is made with <c>VACUUM INTO</c>, which is
/// consistent while the scheduler keeps writing, into a temporary file that is renamed when complete. The first
/// failure raises one alert and the next success after it one recovery alert. Keeps that state in memory, so it is
/// registered as a singleton.
/// </summary>
public sealed class BackupRunner(
    SqliteDatabase database, ISettingsStore settings, AlertSender alerts, TimeProvider time, ILogger<BackupRunner> logger)
{
    private DateTimeOffset? _failingSince;
    private bool _failureAlerted;

    public async Task<BackupResult> RunIfDueAsync(CancellationToken cancellationToken)
    {
        var options = await BackupSettings.LoadAsync(settings, cancellationToken);
        foreach (var problem in options.Problems)
        {
            logger.InvalidSetting(problem);
        }

        if (options.Directory is not { } directory)
        {
            return BackupResult.Disabled;
        }

        var now = time.GetUtcNow();
        string path;
        try
        {
            var existing = BackupFiles.List(directory);
            if (existing.Count > 0 && now - existing[0].TakenAt < options.Interval)
            {
                return BackupResult.NotDue;
            }

            path = await WriteAsync(directory, now, cancellationToken);
            var deleted = BackupFiles.Prune(directory, options.Keep);
            logger.Created(path, deleted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            logger.Failed(ex, directory);
            await ReportFailureAsync(directory, ex.Message, now, cancellationToken);
            return BackupResult.Failed;
        }

        await ReportRecoveryAsync(directory, now, cancellationToken);
        return BackupResult.Created;
    }

    private async Task<string> WriteAsync(string directory, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, BackupFiles.FileName(now));
        var temporary = path + ".tmp";
        File.Delete(temporary);

        await using (var connection = await database.OpenAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "VACUUM INTO $path";
            command.Parameters.AddWithValue("$path", temporary);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        File.Move(temporary, path, overwrite: true);
        return path;
    }

    private async Task ReportFailureAsync(string directory, string error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _failingSince ??= now;
        if (_failureAlerted)
        {
            return;
        }

        var options = await AlertSettings.LoadAsync(settings, cancellationToken);
        var alert = AlertMessages.BackupFailed(directory, error, _failingSince.Value, now);
        _failureAlerted = await alerts.SendAsync(alert, options, cancellationToken) != AlertDispatch.Deferred;
    }

    private async Task ReportRecoveryAsync(string directory, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_failingSince is not { } since)
        {
            return;
        }

        if (_failureAlerted)
        {
            var options = await AlertSettings.LoadAsync(settings, cancellationToken);
            await alerts.SendAsync(AlertMessages.BackupRecovered(directory, since, now), options, cancellationToken);
        }

        _failingSince = null;
        _failureAlerted = false;
    }
}
