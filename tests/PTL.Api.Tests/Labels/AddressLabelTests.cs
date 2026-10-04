using System.Text;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Labels;

public class AddressLabelTests
{
    [Fact]
    public void FromFields_OmitsBlankAndWhitespaceOnlyFields()
    {
        var label = AddressLabel.FromFields("Alice Example", null, string.Empty, "   ", "1 Sample Street");

        Assert.Equal(["Alice Example", "1 Sample Street"], label.Lines);
    }

    [Fact]
    public void FromFields_PreservesOrderAndDoesNotTrim()
    {
        var label = AddressLabel.FromFields(" Alice Example ", "Sample Labs");

        Assert.Equal([" Alice Example ", "Sample Labs"], label.Lines);
    }

    // Legacy LabelCustomerAddress.SetLabelString field order.
    [Fact]
    public void CustomerAddress_UsesLegacyFieldOrder()
    {
        var label = AddressLabelComposer.CustomerAddress(
            "Alice Example", "Sample Labs", "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "United Kingdom");

        Assert.Equal(
            ["Alice Example", "Sample Labs", "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "United Kingdom"],
            label.Lines);
    }

    // Legacy LabelParticipantAddress.SetLabelString appends Telephone after Country.
    [Fact]
    public void ParticipantAddress_AppendsTelephoneAfterCountry()
    {
        var label = AddressLabelComposer.ParticipantAddress(
            "Alice Example", "Sample Labs", "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "United Kingdom", "01234 567890");

        Assert.Equal(
            ["Alice Example", "Sample Labs", "Line 1", "Line 2", "Line 3", "Line 4", "Line 5", "United Kingdom", "01234 567890"],
            label.Lines);
    }

    [Fact]
    public void Render_ProducesAPdfWithASinglePage()
    {
        var pdf = AddressLabelPdfRenderer.Render(AddressLabel.FromFields("Alice Example", "1 Sample Street"));
        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        Assert.Contains("/Type /Page ", text, StringComparison.Ordinal);
        Assert.Contains("/Count 1", text, StringComparison.Ordinal);
        Assert.Contains("startxref", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WritesEachLineAsItsOwnTextRun()
    {
        var pdf = AddressLabelPdfRenderer.Render(AddressLabel.FromFields("Alice Example", "1 Sample Street"));
        var text = Encoding.Latin1.GetString(pdf);

        Assert.Contains("(Alice Example) Tj", text, StringComparison.Ordinal);
        Assert.Contains("(1 Sample Street) Tj", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EscapesCharactersThatWouldTerminateAPdfStringLiteral()
    {
        var pdf = AddressLabelPdfRenderer.Render(AddressLabel.FromFields(@"Smith (Holdings) \ Co"));
        var text = Encoding.Latin1.GetString(pdf);

        Assert.Contains(@"(Smith \(Holdings\) \\ Co) Tj", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EmptyLabelStillProducesAValidPdf()
    {
        var pdf = AddressLabelPdfRenderer.Render(AddressLabel.FromFields(null, string.Empty));
        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.DoesNotContain(") Tj", text, StringComparison.Ordinal);
    }

    // The /Length entry must match the real stream byte count or readers reject the file.
    [Fact]
    public void Render_DeclaredStreamLengthMatchesActualContent()
    {
        var pdf = AddressLabelPdfRenderer.Render(AddressLabel.FromFields("Alice Example", "1 Sample Street"));
        var text = Encoding.Latin1.GetString(pdf);

        var declared = int.Parse(
            text.Split("<< /Length ")[1].Split(" >>")[0],
            System.Globalization.CultureInfo.InvariantCulture);
        var start = text.IndexOf("stream\n", StringComparison.Ordinal) + "stream\n".Length;
        var end = text.IndexOf("\nendstream", StringComparison.Ordinal);

        Assert.Equal(declared, end - start);
    }
}
