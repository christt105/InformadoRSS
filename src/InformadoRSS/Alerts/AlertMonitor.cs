using InformadoRSS.Scheduling;
using InformadoRSS.Storage;

namespace InformadoRSS.Alerts;

/// <summary>
/// Watches every completed feed cycle and raises one alert when a feed or destination becomes broken and one when it
/// recovers. A feed is broken once it has failed continuously for <see cref="AlertSettings.FeedBrokenAfter"/>; a
/// destination is broken while the processor keeps it flagged after a permanent sink failure. What was alerted is
/// stored through <see cref="IAlertStateStore"/>, so restarts do not repeat alerts. An alert whose delivery failed
/// temporarily is retried on the next cycle of the same feed.
/// </summary>
public sealed class AlertMonitor(
    IFeedStore feeds, IAlertStateStore states, ISettingsStore settings, AlertSender sender, TimeProvider time) : IFeedCycleObserver
{
    public async Task OnFeedProcessedAsync(Feed feed, FeedOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);

        if (await feeds.GetAsync(feed.Id, cancellationToken) is not { } current)
        {
            return;
        }

        var options = await AlertSettings.LoadAsync(settings, cancellationToken);
        await CheckFeedAsync(current, options, cancellationToken);
        await CheckDestinationsAsync(current, options, cancellationToken);
    }

    private async Task CheckFeedAsync(Feed feed, AlertSettings options, CancellationToken cancellationToken)
    {
        var state = await states.GetFeedStateAsync(feed.Id, cancellationToken);
        var now = time.GetUtcNow();

        if (feed.Health.ConsecutiveFailures == 0)
        {
            if (state is null)
            {
                return;
            }

            if (state.AlertedAt is not null
                && await sender.SendAsync(AlertMessages.FeedRecovered(feed, state.FailingSince, now), options, cancellationToken)
                    == AlertDispatch.Deferred)
            {
                return;
            }

            await states.ClearFeedStateAsync(feed.Id, cancellationToken);
            return;
        }

        if (state is null)
        {
            state = new FeedAlertState(feed.Health.LastErrorAt ?? now, null);
            await states.SetFeedStateAsync(feed.Id, state, cancellationToken);
        }

        if (state.AlertedAt is null
            && now - state.FailingSince >= options.FeedBrokenAfter
            && await sender.SendAsync(AlertMessages.FeedBroken(feed, state, now), options, cancellationToken) != AlertDispatch.Deferred)
        {
            await states.SetFeedStateAsync(feed.Id, state with { AlertedAt = now }, cancellationToken);
        }
    }

    private async Task CheckDestinationsAsync(Feed feed, AlertSettings options, CancellationToken cancellationToken)
    {
        foreach (var destination in await states.ListDestinationStatesAsync(feed.Id, cancellationToken))
        {
            var now = time.GetUtcNow();
            if (destination.BrokenSince is not null && destination.AlertedAt is null)
            {
                if (await sender.SendAsync(AlertMessages.DestinationBroken(feed, destination, now), options, cancellationToken)
                    != AlertDispatch.Deferred)
                {
                    await states.SetDestinationAlertedAsync(destination.DestinationId, now, cancellationToken);
                }
            }
            else if (destination.BrokenSince is null && destination.AlertedAt is not null)
            {
                if (await sender.SendAsync(AlertMessages.DestinationRecovered(feed, destination, now), options, cancellationToken)
                    != AlertDispatch.Deferred)
                {
                    await states.SetDestinationAlertedAsync(destination.DestinationId, null, cancellationToken);
                }
            }
        }
    }
}
