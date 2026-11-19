using System.Text;
using PTL.SharedUI.RichText;

namespace PTL.SharedUI.Tests.RichText;

public class RichTextTransportTests
{
    private static string Wire(string html) => RichTextTransport.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

    [Theory]
    [InlineData("<p>Hello <strong>world</strong></p>")]
    [InlineData("<p>caf\u00e9 \u00b5g/mL &amp; &nbsp; \u2013 \U0001F600</p>")]
    [InlineData("<script>alert(1)</script>")]
    public void Decode_PostedBase64_ReturnsTheHtml(string html) =>
        Assert.Equal(html, RichTextTransport.Decode(Wire(html)));

    [Fact]
    public void Decode_LargeValue_RoundTrips()
    {
        var html = "<p>" + new string('x', 60_000) + "</p>";

        Assert.Equal(html, RichTextTransport.Decode(Wire(html)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p>plain post with scripting off</p>")]
    public void Decode_ValueWithoutThePrefix_IsReturnedUnchanged(string? posted) =>
        Assert.Equal(posted, RichTextTransport.Decode(posted));

    [Fact]
    public void Decode_PrefixedButNotBase64_IsReturnedUnchanged()
    {
        const string posted = RichTextTransport.Prefix + "not base64!";

        Assert.Equal(posted, RichTextTransport.Decode(posted));
    }

    [Fact]
    public void Decode_PrefixOnly_IsEmpty() =>
        Assert.Equal(string.Empty, RichTextTransport.Decode(RichTextTransport.Prefix));

    [Theory]
    [InlineData("<p>Hello <strong>world</strong></p>")]
    [InlineData("<p>caf\u00e9 \u00b5g/mL &amp; \u2013 \U0001F600</p>")]
    public void Encode_ThenDecode_ReturnsTheOriginalHtml(string html)
    {
        var wire = RichTextTransport.Encode(html);

        Assert.StartsWith(RichTextTransport.Prefix, wire, StringComparison.Ordinal);
        Assert.DoesNotContain("<", wire, StringComparison.Ordinal);
        Assert.Equal(html, RichTextTransport.Decode(wire));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encode_NullOrEmpty_IsEmpty(string? html) =>
        Assert.Equal(string.Empty, RichTextTransport.Encode(html));

    [Fact]
    public void RichTextBinderAttribute_UsesTheRichTextModelBinder() =>
        Assert.Equal(typeof(RichTextModelBinder), new RichTextBinderAttribute().BinderType);

    [Fact]
    public async Task RichTextModelBinder_NullContext_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => new RichTextModelBinder().BindModelAsync(null!));
}
