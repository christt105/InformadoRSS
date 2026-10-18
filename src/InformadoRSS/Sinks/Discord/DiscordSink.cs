using System.Net;
using InformadoRSS.Core;

namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Posts messages to a Discord channel as the bot. Destination parameters: <see cref="ChannelIdParameter"/>
/// (required) and <see cref="GuildIdParameter"/> (informative, for the UI).
/// </summary>
public sealed class DiscordSink(DiscordRestClient client) : ISink
{
    public const string SinkName = "discord";
    public const string ChannelIdParameter = "channelId";
    public const string GuildIdParameter = "guildId";

    private static readonly HashSet<int> DestinationErrorCodes =
    [
        10003, // Unknown channel
        50001, // Missing access
        50013, // Missing permissions
    ];

    public string Name => SinkName;

    public async Task<DeliveryResult> DeliverAsync(Destination destination, RenderedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(message);

        if (!destination.Parameters.TryGetValue(ChannelIdParameter, out var channelId) || !IsSnowflake(channelId))
        {
            return DeliveryResult.Permanent($"Discord destination needs a numeric \"{ChannelIdParameter}\" parameter");
        }

        var response = await client.CreateMessageAsync(channelId, ToPayload(message), cancellationToken);
        return ToDeliveryResult(response, channelId);
    }

    public static DiscordMessagePayload ToPayload(RenderedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var embed = message.Embed;
        return new DiscordMessagePayload(
            string.IsNullOrEmpty(message.Content) ? null : message.Content,
            embed is null
                ? null
                :
                [
                    new DiscordEmbedPayload(
                        Title: embed.Title,
                        Description: embed.Description,
                        Url: embed.Url,
                        Color: embed.Color,
                        Timestamp: embed.Timestamp,
                        Image: embed.ImageUrl is null ? null : new DiscordEmbedMedia(embed.ImageUrl),
                        Thumbnail: embed.ThumbnailUrl is null ? null : new DiscordEmbedMedia(embed.ThumbnailUrl),
                        Footer: embed.Footer is null ? null : new DiscordEmbedFooter(embed.Footer),
                        Author: embed.Author is null ? null : new DiscordEmbedAuthor(embed.Author)),
                ],
            DiscordAllowedMentions.None);
    }

    private static DeliveryResult ToDeliveryResult(DiscordApiResponse response, string channelId)
    {
        switch (response.Outcome)
        {
            case DiscordApiOutcome.Success:
                return DeliveryResult.Delivered;
            case DiscordApiOutcome.MissingToken:
                return DeliveryResult.Transient("Discord bot token not configured");
            case DiscordApiOutcome.RateLimited:
                return DeliveryResult.Transient("rate limited by Discord", response.RetryAfter);
            case DiscordApiOutcome.NetworkError:
                return DeliveryResult.Transient(response.Message ?? "could not reach Discord");
        }

        var detail = Describe(response);
        if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
        {
            return DeliveryResult.Transient($"Discord rejected the bot token, check it in the settings ({detail})");
        }

        if (response.StatusCode is (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.NotFound
            || response.ErrorCode is { } code && DestinationErrorCodes.Contains(code))
        {
            return DeliveryResult.Permanent($"Discord channel {channelId} is not usable by the bot ({detail})");
        }

        if (response.StatusCode is >= 500 or null or (int)HttpStatusCode.RequestTimeout)
        {
            return DeliveryResult.Transient($"Discord is unavailable ({detail})");
        }

        return DeliveryResult.Permanent($"Discord rejected the message ({detail})");
    }

    private static string Describe(DiscordApiResponse response)
    {
        var parts = new List<string>(3);
        if (response.StatusCode is { } status)
        {
            parts.Add($"HTTP {status}");
        }

        if (response.ErrorCode is { } code and not 0)
        {
            parts.Add($"code {code}");
        }

        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            parts.Add(response.Message);
        }

        return string.Join(", ", parts);
    }

    private static bool IsSnowflake(string value) => value.Length is > 0 and <= 20 && value.All(char.IsAsciiDigit);
}
