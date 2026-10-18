namespace InformadoRSS.Core;

/// <summary>A target for a sink, e.g. a Discord channel or an ntfy topic.</summary>
public sealed record Destination(string Sink, IReadOnlyDictionary<string, string> Parameters);
