using System.Globalization;
using InformadoRSS.Sources;

namespace InformadoRSS.Tests.Sources;

public sealed class FeedDateTests
{
    [Theory]
    [InlineData("Fri, 16 Oct 2026 07:29:25 -0700", "2026-10-16T07:29:25-07:00")]
    [InlineData("Fri, 16 Oct 2026 00:00:00 +0200", "2026-10-16T00:00:00+02:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09 +0000", "2026-10-12T14:20:09+00:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09 GMT", "2026-10-12T14:20:09+00:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09 UT", "2026-10-12T14:20:09+00:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09 EST", "2026-10-12T14:20:09-05:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09 PDT", "2026-10-12T14:20:09-07:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09 CEST", "2026-10-12T14:20:09+02:00")]
    [InlineData("12 Oct 2026 14:20 +0100", "2026-10-12T14:20:00+01:00")]
    [InlineData("Monday, 12 October 2026 14:20:09 +01:00", "2026-10-12T14:20:09+01:00")]
    [InlineData("Mon, 2 Feb 26 08:00:00 Z", "2026-02-02T08:00:00+00:00")]
    [InlineData("Mon, 12 Oct 2026 14:20:09", "2026-10-12T14:20:09+00:00")]
    [InlineData("Tue, 13 Oct 2026 14:20:09 XYZ", "2026-10-13T14:20:09+00:00")]
    [InlineData("12-Oct-2026 14:20:09 +0000", "2026-10-12T14:20:09+00:00")]
    [InlineData("Oct 12 2026 14:20:09 GMT", "2026-10-12T14:20:09+00:00")]
    [InlineData("  Mon,  12 Oct 2026  14:20:09  +0000 ", "2026-10-12T14:20:09+00:00")]
    [InlineData("2026-10-17T10:02:25.455Z", "2026-10-17T10:02:25.455+00:00")]
    [InlineData("2026-09-02T17:00:32+00:00", "2026-09-02T17:00:32+00:00")]
    [InlineData("2026-09-02T17:00:32-03:30", "2026-09-02T17:00:32-03:30")]
    [InlineData("2026-09-02T17:00:32", "2026-09-02T17:00:32+00:00")]
    [InlineData("2026-09-02 17:00:32", "2026-09-02T17:00:32+00:00")]
    [InlineData("2026-09-02", "2026-09-02T00:00:00+00:00")]
    public void ParsesKnownFormats(string value, string expected)
    {
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), FeedDate.Parse(value));
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture).Offset, FeedDate.Parse(value)!.Value.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yesterday")]
    [InlineData("Mon, 31 Feb 2026 10:00:00 GMT")]
    [InlineData("Mon, 12 Foo 2026 10:00:00 GMT")]
    public void ReturnsNullForUnparsableValues(string? value)
    {
        Assert.Null(FeedDate.Parse(value));
    }
}
