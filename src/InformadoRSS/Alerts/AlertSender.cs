using System.Net;
using System.Text.Json;
using InformadoRSS.Core;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;

namespace InformadoRSS.Alerts;

/// <summary>
/// Logs every alert and delivers it to the target chosen in <see cref="AlertSettings"/>: a Discord channel, a Discord
/// direct message (opened through <c>POST /users/@me/channels</c>), or an ntfy topic.
/// </summary>
public sealed class AlertSender(DiscordRestClient discord, NtfyClient ntfy, ILogger<AlertSender> logger)
{
    public const int ProblemColor = 0xED4245;
    public const int RecoveryColor = 0x57F287;

    public async Task<AlertDispatch> SendAsync(Alert alert, AlertSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(settings);

        if (alert.IsProblem)
        {
            logger.ProblemRaised(alert.Title, alert.Message);
        }
        else
        {
            logger.RecoveryRaised(alert.Title, alert.Message);
        }

        foreach (var problem in settings.Problems)
        {
            logger.InvalidSetting(problem);
        }

        return settings.Target == AlertTarget.Ntfy
            ? await SendToNtfyAsync(alert, settings, cancellationToken)
            : await SendToDiscordAsync(alert, settings, cancellationToken);
    }

    private async Task<AlertDispatch> SendToDiscordAsync(Alert alert, AlertSettings settings, CancellationToken cancellationToken)
    {
        var channelId = settings.DiscordChannelId;
        if (channelId is null && settings.DiscordUserId is { } userId)
        {
            var response = await discord.CreateDmChannelAsync(userId, cancellationToken);
            channelId = response.IsSuccess ? ReadChannelId(response.Body) : null;
            if (channelId is null)
            {
                return Report(AlertTarget.Discord, ToDeliveryResult(response, userId));
            }
        }

        if (channelId is null)
        {
            logger.NoTarget(AlertTarget.Discord);
            return AlertDispatch.Logged;
        }

        var destination = new Destination(
            DiscordSink.SinkName, new Dictionary<string, string> { [DiscordSink.ChannelIdParameter] = channelId });
        var message = new RenderedMessage(
            string.Empty,
            new RenderedEmbed(
                Title: alert.Title,
                Description: alert.Message,
                Color: alert.IsProblem ? ProblemColor : RecoveryColor,
                Timestamp: alert.At));
        var result = await new DiscordSink(discord).DeliverAsync(destination, message, cancellationToken);
        return Report(AlertTarget.Discord, result);
    }

    private async Task<AlertDispatch> SendToNtfyAsync(Alert alert, AlertSettings settings, CancellationToken cancellationToken)
    {
        if (settings.NtfyTopic is not { } topic)
        {
            logger.NoTarget(AlertTarget.Ntfy);
            return AlertDispatch.Logged;
        }

        var message = new NtfyMessage(
            alert.Message,
            Title: alert.Title,
            Priority: alert.IsProblem ? 4 : 3,
            Tags: [alert.IsProblem ? "warning" : "white_check_mark"]);
        var result = await ntfy.PublishAsync(topic, message, cancellationToken);
        return Report(AlertTarget.Ntfy, result);
    }

    private AlertDispatch Report(AlertTarget target, DeliveryResult result)
    {
        switch (result.Status)
        {
            case DeliveryStatus.Delivered:
                logger.Sent(target);
                return AlertDispatch.Sent;
            case DeliveryStatus.Transient:
                logger.Deferred(target, result.Error);
                return AlertDispatch.Deferred;
            default:
                logger.Failed(target, result.Error);
                return AlertDispatch.Failed;
        }
    }

    private static DeliveryResult ToDeliveryResult(DiscordApiResponse response, string userId)
    {
        var temporary = response.Outcome is DiscordApiOutcome.MissingToken or DiscordApiOutcome.RateLimited or DiscordApiOutcome.NetworkError
            || response.StatusCode is (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.RequestTimeout or >= 500;
        var error = response.IsSuccess
            ? $"Discord returned no channel for the direct message to user {userId}"
            : $"could not open a direct message with Discord user {userId} ({response.Message ?? $"HTTP {response.StatusCode}"})";
        return temporary ? DeliveryResult.Transient(error) : DeliveryResult.Permanent(error);
    }

    private static string? ReadChannelId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, DiscordJsonContext.Default.DiscordChannel)?.Id;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
