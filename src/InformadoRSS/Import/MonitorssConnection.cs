using System.Text.Json;

namespace InformadoRSS.Import;

/// <summary>A MonitoRSS <c>connections.discordChannels[]</c> entry.</summary>
/// <param name="ChannelType">
/// <c>details.channel.type</c>: <c>null</c> for a plain text channel, otherwise a forum or thread variant.
/// </param>
/// <param name="WebhookChannelId">The channel behind <c>details.webhook</c>, for webhook connections.</param>
/// <param name="Content">The message text template, <c>details.content</c>.</param>
/// <param name="FilterExpression"><c>filters.expression</c>, or <c>null</c> when the connection has no filter.</param>
/// <param name="EmbedCount">How many entries <c>details.embeds</c> has.</param>
/// <param name="HasComponents">Whether the connection has buttons (<c>componentRows</c>) or a components V2 layout.</param>
/// <param name="HasThreadOptions">Whether the connection creates threads or forum posts.</param>
/// <param name="IgnoredOptions">Options that are dropped without changing where or whether items are sent.</param>
public sealed record MonitorssConnection(
    string? Id,
    string Name,
    string? DisabledCode,
    string? ChannelId,
    string? GuildId,
    string? ChannelType,
    string? WebhookChannelId,
    string? Content,
    JsonElement? FilterExpression,
    int EmbedCount,
    bool HasComponents,
    bool HasThreadOptions,
    IReadOnlyList<string> IgnoredOptions);
