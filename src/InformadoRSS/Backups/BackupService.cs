namespace InformadoRSS.Backups;

/// <summary>Asks the <see cref="BackupRunner"/> for a backup at startup and then every <see cref="CheckInterval"/>.</summary>
public sealed class BackupService(BackupRunner runner, TimeProvider time, ILogger<BackupService> logger) : BackgroundService
{
    /// <summary>How often the runner checks whether a backup is due, which also paces retries after a failure.</summary>
    public static TimeSpan CheckInterval { get; } = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await runner.RunIfDueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.CheckCrashed(ex);
                }

                await Task.Delay(CheckInterval, time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
