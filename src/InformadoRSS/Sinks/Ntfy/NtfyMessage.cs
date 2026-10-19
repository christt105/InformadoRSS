namespace InformadoRSS.Sinks.Ntfy;

/// <summary>A notification to publish to an ntfy topic.</summary>
/// <param name="Body">The message text, sent as the request body.</param>
/// <param name="Priority">From 1 (min) to 5 (max); <c>null</c> for the server default (3).</param>
/// <param name="Tags">Tags or emoji shortcodes such as <c>warning</c>.</param>
/// <param name="Click">A URL opened when the notification is tapped.</param>
public sealed record NtfyMessage(
    string Body,
    string? Title = null,
    int? Priority = null,
    IReadOnlyList<string>? Tags = null,
    string? Click = null);
