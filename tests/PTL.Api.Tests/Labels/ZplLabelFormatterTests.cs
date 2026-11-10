using System.Text;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Labels;

public class ZplLabelFormatterTests
{
    [Fact]
    public void ToZpl_WrapsTheLabelInStartAndEndFormatCommands()
    {
        var label = AddressLabel.FromFields("Alice Example");

        var zpl = Encoding.ASCII.GetString(ZplLabelFormatter.ToZpl(label));

        Assert.StartsWith("^XA", zpl, StringComparison.Ordinal);
        Assert.EndsWith("^XZ\n", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void ToZpl_EmitsOneFieldCommandPerLabelLine()
    {
        var label = AddressLabel.FromFields("Alice Example", "1 Sample Street", "United Kingdom");

        var zpl = Encoding.ASCII.GetString(ZplLabelFormatter.ToZpl(label));

        Assert.Contains("^FH^FDAlice Example^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("^FH^FD1 Sample Street^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("^FH^FDUnited Kingdom^FS", zpl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("A^B", "A_5EB")]
    [InlineData("A~B", "A_7EB")]
    [InlineData("A_B", "A_5FB")]
    public void ToZpl_HexEscapesCaretTildeAndTheHexIndicatorCharacter(string line, string expectedEscapedFieldData)
    {
        var label = AddressLabel.FromFields(line);

        var zpl = Encoding.ASCII.GetString(ZplLabelFormatter.ToZpl(label));

        Assert.Contains($"^FH^FD{expectedEscapedFieldData}^FS", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void ToZpl_NullLabel_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ZplLabelFormatter.ToZpl(null!));
    }
}
