using System.Diagnostics.CodeAnalysis;

namespace InformadoRSS.Sinks.Discord;

/// <summary>Either a value read from Discord or the failed <see cref="DiscordApiResponse"/> explaining why there is none.</summary>
public sealed record DiscordResult<T>(T? Value, DiscordApiResponse? Failure)
    where T : class
{
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSuccess => Value is not null;
}
