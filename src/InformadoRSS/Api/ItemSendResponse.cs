namespace InformadoRSS.Api;

/// <param name="Errors">One entry per destination that did not accept the message.</param>
public sealed record ItemSendResponse(bool Sent, IReadOnlyList<string> Errors);
