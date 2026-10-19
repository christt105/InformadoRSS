namespace InformadoRSS.Import;

/// <param name="TestChannelId">When set, every imported destination posts to this Discord channel instead of its own.</param>
/// <param name="TestGuildId">
/// The server of <paramref name="TestChannelId"/>, stored as the destination guild instead of the original one. Without
/// it, redirected destinations carry no guild.
/// </param>
/// <param name="DryRun">Computes the report without writing anything.</param>
public sealed record ImportOptions(string? TestChannelId = null, bool DryRun = false, string? TestGuildId = null);
