using System.Text;

namespace InformadoRSS.Sources;

/// <summary>
/// Composes Latin letters followed by a combining accent (e.g. <c>a</c> + U+0301) into the precomposed character (<c>á</c>).
/// <see cref="string.Normalize()"/> does not do it under invariant globalization, and some feeds
/// (e.g. newsletters) send decomposed text, which would break plain "contains" filters.
/// </summary>
internal static class LatinComposition
{
    private static readonly Dictionary<(char Letter, char Mark), char> Table = Build(
        ('\u0300', "AÀEÈIÌOÒUÙaàeèiìoòuù"),
        ('\u0301', "AÁEÉIÍOÓUÚYÝaáeéiíoóuúyýCĆcćLĹlĺNŃnńRŔrŕSŚsśZŹzź"),
        ('\u0302', "AÂEÊIÎOÔUÛaâeêiîoôuûCĈcĉGĜgĝHĤhĥJĴjĵSŜsŝWŴwŵYŶyŷ"),
        ('\u0303', "AÃNÑOÕaãnñoõIĨiĩUŨuũ"),
        ('\u0304', "AĀaāEĒeēIĪiīOŌoōUŪuū"),
        ('\u0306', "AĂaăEĔeĕGĞgğIĬiĭOŎoŏUŬuŭ"),
        ('\u0307', "CĊcċEĖeėGĠgġIİZŻzż"),
        ('\u0308', "AÄEËIÏOÖUÜaäeëiïoöuüyÿYŸ"),
        ('\u030A', "AÅaåUŮuů"),
        ('\u030B', "OŐoőUŰuű"),
        ('\u030C', "CČcčDĎdďEĚeěLĽlľNŇnňRŘrřSŠsšTŤtťZŽzž"),
        ('\u0327', "CÇcçGĢgģKĶkķLĻlļNŅnņRŖrŗSŞsşTŢtţ"),
        ('\u0328', "AĄaąEĘeęIĮiįUŲuų"));

    public static string Compose(string text)
    {
        if (!text.AsSpan().ContainsAnyInRange('\u0300', '\u036F'))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (builder.Length > 0 && Table.TryGetValue((builder[^1], c), out var composed))
            {
                builder[^1] = composed;
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static Dictionary<(char Letter, char Mark), char> Build(params (char Mark, string Pairs)[] marks)
    {
        var table = new Dictionary<(char Letter, char Mark), char>();
        foreach (var (mark, pairs) in marks)
        {
            for (var i = 0; i + 1 < pairs.Length; i += 2)
            {
                table[(pairs[i], mark)] = pairs[i + 1];
            }
        }

        return table;
    }
}
