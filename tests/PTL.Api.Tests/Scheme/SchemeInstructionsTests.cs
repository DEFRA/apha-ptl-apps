using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

// Legacy TBEdit: its Text setter stripped auto-inserted hyperlinks and was meant to restrict the
// markup; its TextPlain property fed the required-field check. The allow-list is sized from the
// tags present in live fldInstructions data.
public class SchemeInstructionsTests
{
    [Theory]
    [InlineData("<p>Store at 4&#xb0;C</p>")]
    [InlineData("<strong>Note:</strong><br><em>test</em>")]
    [InlineData("<b>a</b><i>b</i><u>c</u><sup>2</sup><sub>3</sub>")]
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

    [Fact]
    public void Sanitise_LeavesNamedEntitiesAlone()
    {
        Assert.Equal("<p>4&deg;C&nbsp;max</p>", SchemeInstructions.Sanitise("<p>4&deg;C&nbsp;max</p>"));
    }

    [Fact]
    public void Sanitise_CollapsesTagDelimitersAccidentallyDoubleEncodedByARoundTrip()
    {
        const string doubleEncoded = "<p>&lt;p&gt;Store at 4 &amp;deg;C&lt;/p&gt;</p>";

        Assert.Equal("<p>Store at 4 &deg;C</p>", SchemeInstructions.Sanitise(doubleEncoded));
    }

    [Fact]
    public void Sanitise_CollapsesMultipleAccumulatedLayersOfEncoding()
    {
        const string tripleEncoded = "<p>&lt;p&gt;&amp;lt;p&amp;gt;Store at 4 &amp;amp;deg;C&amp;lt;/p&amp;gt;&lt;/p&gt;</p>";

        Assert.Equal("<p>Store at 4 &deg;C</p>", SchemeInstructions.Sanitise(tripleEncoded));
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
