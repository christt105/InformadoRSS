using InformadoRSS.Sinks.Discord;

namespace InformadoRSS.Tests.Sinks.Discord;

public sealed class MutableTokenProvider(string? token) : IDiscordTokenProvider
{
    public string? Token { get; set; } = token;

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult(Token);
}
