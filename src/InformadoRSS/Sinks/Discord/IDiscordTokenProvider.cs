namespace InformadoRSS.Sinks.Discord;

/// <summary>Supplies the bot token for each Discord request, so a token changed in the settings applies right away.</summary>
public interface IDiscordTokenProvider
{
    /// <returns>The raw token (without the <c>Bot </c> prefix), or <see langword="null"/> when none is configured.</returns>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken);
}
