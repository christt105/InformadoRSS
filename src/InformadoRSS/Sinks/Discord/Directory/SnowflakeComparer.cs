namespace InformadoRSS.Sinks.Discord;

/// <summary>Orders Discord ids numerically (shorter decimal strings first), with <see langword="null"/> first.</summary>
public sealed class SnowflakeComparer : IComparer<string?>
{
    public static SnowflakeComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return x is null ? (y is null ? 0 : -1) : 1;
        }

        var byLength = x.Length.CompareTo(y.Length);
        return byLength != 0 ? byLength : string.CompareOrdinal(x, y);
    }

    /// <summary>Whether <paramref name="value"/> looks like a Discord id: 1 to 20 ASCII digits.</summary>
    public static bool IsSnowflake(string? value) => value is { Length: > 0 and <= 20 } && value.All(char.IsAsciiDigit);
}
