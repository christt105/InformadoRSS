using System.Globalization;

namespace InformadoRSS.Sources;

/// <summary>Lenient parser for the date formats found in real feeds: RFC 822/1123 variants and ISO 8601.</summary>
public static class FeedDate
{
    private static readonly string[] MonthNames =
        ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    private static readonly Dictionary<string, int> ZoneOffsetHours = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UT"] = 0, ["UTC"] = 0, ["GMT"] = 0, ["Z"] = 0,
        ["EST"] = -5, ["EDT"] = -4, ["CST"] = -6, ["CDT"] = -5,
        ["MST"] = -7, ["MDT"] = -6, ["PST"] = -8, ["PDT"] = -7,
        ["AKST"] = -9, ["AKDT"] = -8, ["HST"] = -10,
        ["WET"] = 0, ["WEST"] = 1, ["BST"] = 1, ["CET"] = 1, ["CEST"] = 2, ["EET"] = 2, ["EEST"] = 3,
        ["MSK"] = 3, ["JST"] = 9, ["KST"] = 9, ["AEST"] = 10, ["AEDT"] = 11,
    };

    private static readonly string[] IsoFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd HH:mm:ssK",
        "yyyy-MM-dd",
    ];

    /// <summary>Parses <paramref name="value"/>; dates without a time zone are taken as UTC.</summary>
    public static DateTimeOffset? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (DateTimeOffset.TryParseExact(trimmed, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso))
        {
            return iso;
        }

        if (TryParseRfc822(trimmed, out var rfc))
        {
            return rfc;
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var any)
            ? any
            : null;
    }

    private static bool TryParseRfc822(string value, out DateTimeOffset result)
    {
        result = default;

        var comma = value.IndexOf(',', StringComparison.Ordinal);
        var rest = comma >= 0 && value[..comma].All(char.IsAsciiLetter) ? value[(comma + 1)..] : value;
        var tokens = rest.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count > 0 && tokens[0].Contains('-', StringComparison.Ordinal))
        {
            var dateParts = tokens[0].Split('-', StringSplitOptions.RemoveEmptyEntries);
            tokens.RemoveAt(0);
            tokens.InsertRange(0, dateParts);
        }

        if (tokens.Count < 3)
        {
            return false;
        }

        if (!TryParseDayAndMonth(tokens[0], tokens[1], out var day, out var month))
        {
            return false;
        }

        if (!int.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (tokens[2].Length <= 2)
        {
            year += year < 50 ? 2000 : 1900;
        }

        var time = TimeSpan.Zero;
        var zoneIndex = 3;
        if (tokens.Count > 3 && tokens[3].Contains(':', StringComparison.Ordinal))
        {
            if (!TimeSpan.TryParseExact(tokens[3], ["h\\:m\\:s", "h\\:m", "h\\:m\\:s\\.FFFFFFF"], CultureInfo.InvariantCulture, out time))
            {
                return false;
            }

            zoneIndex = 4;
        }

        var offset = TimeSpan.Zero;
        if (tokens.Count > zoneIndex && !TryParseZone(tokens[zoneIndex], out offset))
        {
            return false;
        }

        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || time >= TimeSpan.FromDays(1))
        {
            return false;
        }

        result = new DateTimeOffset(new DateTime(year, month, day).Add(time), offset);
        return true;
    }

    private static bool TryParseDayAndMonth(string first, string second, out int day, out int month)
    {
        if (int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out day))
        {
            return TryParseMonth(second, out month);
        }

        return TryParseMonth(first, out month) && int.TryParse(second, NumberStyles.None, CultureInfo.InvariantCulture, out day);
    }

    private static bool TryParseMonth(string token, out int month)
    {
        month = 0;
        if (token.Length < 3)
        {
            return false;
        }

        var index = Array.IndexOf(MonthNames, token[..3].ToLowerInvariant());
        month = index + 1;
        return index >= 0;
    }

    private static bool TryParseZone(string token, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;

        if (ZoneOffsetHours.TryGetValue(token, out var hours))
        {
            offset = TimeSpan.FromHours(hours);
            return true;
        }

        var sign = token[0] switch
        {
            '+' => 1,
            '-' => -1,
            _ => 0,
        };

        if (sign == 0)
        {
            return token.All(char.IsAsciiLetter);
        }

        var digits = token[1..].Replace(":", string.Empty, StringComparison.Ordinal);
        if (digits.Length is not (2 or 4) || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var h = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var m = digits.Length == 4 ? int.Parse(digits[2..], CultureInfo.InvariantCulture) : 0;
        offset = sign * new TimeSpan(h, m, 0);
        return offset.Duration() <= TimeSpan.FromHours(14);
    }
}
