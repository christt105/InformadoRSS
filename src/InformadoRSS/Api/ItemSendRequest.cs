namespace InformadoRSS.Api;

/// <param name="ItemId">Id of the item, as listed by the items endpoint.</param>
/// <param name="RouteId">Route whose template renders the message.</param>
/// <param name="ChannelId">Discord channel to send to instead of the route destinations.</param>
public sealed record ItemSendRequest(string? ItemId, long? RouteId, string? ChannelId);
