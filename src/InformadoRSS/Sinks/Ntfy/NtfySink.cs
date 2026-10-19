using InformadoRSS.Core;

namespace InformadoRSS.Sinks.Ntfy;

/// <summary>
/// Publishes messages to an ntfy topic on the configured server. Destination parameter: <see cref="TopicParameter"/>
/// (required). The message text becomes the body; an embed contributes its title, its URL as the click action, and its
/// description when there is no text.
/// </summary>
public sealed class NtfySink(NtfyClient client) : ISink
{
    public const string SinkName = "ntfy";
    public const string TopicParameter = "topic";

    public string Name => SinkName;

    public Task<DeliveryResult> DeliverAsync(Destination destination, RenderedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(message);

        if (!destination.Parameters.TryGetValue(TopicParameter, out var topic) || !NtfySettings.IsValidTopic(topic))
        {
            return Task.FromResult(DeliveryResult.Permanent($"ntfy destination needs a valid \"{TopicParameter}\" parameter"));
        }

        return client.PublishAsync(topic, ToMessage(message), cancellationToken);
    }

    public static NtfyMessage ToMessage(RenderedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var embed = message.Embed;
        var body = !string.IsNullOrWhiteSpace(message.Content)
            ? message.Content
            : embed?.Description ?? embed?.Url ?? embed?.Title ?? string.Empty;
        return new NtfyMessage(body, Title: embed?.Title, Click: embed?.Url);
    }
}
