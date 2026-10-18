namespace InformadoRSS.Scheduling;

/// <summary>Hosts the <see cref="FeedScheduler"/> loop, unless <see cref="SchedulerOptions.Enabled"/> is off.</summary>
public sealed class SchedulerService(
    FeedScheduler scheduler, SchedulerOptions options, TimeProvider time, ILogger<SchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.Disabled();
            return;
        }

        logger.Started(options.MaxConcurrency, options.MaxItemsPerRoute);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await scheduler.StartDueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.CycleFailed(ex);
                }

                await Task.Delay(options.PollInterval, time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await scheduler.WhenIdleAsync();
        }
    }
}
