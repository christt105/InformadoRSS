using InformadoRSS.Sources;

namespace InformadoRSS.Tests.Sources;

public sealed class HtmlTextTests
{
    [Theory]
    [InlineData("<p>Hello <b>world</b></p>", "Hello world")]
    [InlineData("Fish &amp; chips &lt;3 &rsquo;&#8217;&#x2019;", "Fish & chips <3 ’’’")]
    [InlineData("<p>One</p><p>Two</p>", "One\n\nTwo")]
    [InlineData("line<br>break<br/>again", "line\nbreak\nagain")]
    [InlineData("<ul><li>a</li><li>b</li></ul>after", "a\nb\n\nafter")]
    [InlineData("  lots \n\t of   <i>space</i>  ", "lots of space")]
    [InlineData("<style>p { color: red; }</style><script>alert('<p>')</script>Visible", "Visible")]
    [InlineData("<html><head><title>Hidden</title></head><body>Shown</body></html>", "Shown")]
    [InlineData("a <!-- comment <p> --> b", "a b")]
    [InlineData("<a href=\"x\" title=\"a > b\">link</a>", "link")]
    [InlineData("1 < 2 and 3 > 2", "1 < 2 and 3 > 2")]
    [InlineData("pre\u200C\u200C\u00A0\u200Cheader", "pre header")]
    [InlineData("<p>A</p>\n\n\n<div></div><div></div><p>B</p>", "A\n\nB")]
    [InlineData("<p>Sa\u0301bado, n\u0303u, c\u0327a, u\u0308</p>", "S\u00E1bado, \u00F1u, \u00E7a, \u00FC")]
    [InlineData("", "")]
    public void ToPlainText(string html, string expected)
    {
        Assert.Equal(expected, HtmlText.ToPlainText(html));
    }

    [Fact]
    public void NormalizeTextKeepsLineBreaks()
    {
        Assert.Equal("a b\nc\n\nd", HtmlText.NormalizeText("  a   b \r\nc\n\n\n\n d  "));
    }

    [Fact]
    public void FromPlainTextEscapesMarkupAndLineBreaks()
    {
        Assert.Equal("a &lt;b&gt; &amp; &quot;c&quot;<br>é", HtmlText.FromPlainText("a <b> & \"c\"\r\né"));
    }
}
