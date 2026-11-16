using System.Globalization;
using System.Text;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Labels;

public class FingerprintLabelFormatterTests
{
    private static string[] Statements(AddressLabel label) =>
        Encoding.ASCII.GetString(FingerprintLabelFormatter.ToFingerprint(label)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void ToFingerprint_EnablesDirectProtocolAndVerboseModeClearsTheLayoutSelectsTheFontPrintsAndRestoresTheModes()
    {
        var statements = Statements(AddressLabel.FromFields("Alice Example"));

        Assert.Equal(["INPUT ON", "VERBON", "CLL", "FT \"Univers Bold\",10"], statements[..4]);
        Assert.Equal(["PF", "VERBOFF", "INPUT OFF"], statements[^3..]);
    }

    // The PC43d's configured media is 75mm x 50.13mm, which at 203 dpi (8 dots/mm) is 600 x 401 dots. Fingerprint
    // measures up from the bottom of the label, so the first line sits at 401 - 26 = 375 and each line steps down 32.
    [Fact]
    public void ToFingerprint_EmitsOnePositionAndTextStatementPerLine_StartingAtTheTopLeftAndStepsDown()
    {
        var statements = Statements(AddressLabel.FromFields("Alice Example", "1 Sample Street", "United Kingdom"));

        Assert.Equal(
            ["PP 0,375:PT \"Alice Example\"", "PP 0,343:PT \"1 Sample Street\"", "PP 0,311:PT \"United Kingdom\""],
            statements[4..^3]);
    }

    [Fact]
    public void ToFingerprint_AFullParticipantLabel_PlacesEveryLineInsideTheLabel()
    {
        var label = new AddressLabel(["1", "2", "3", "4", "5", "6", "7", "8", "9"]);

        var positions = Statements(label)[4..^3]
            .Select(statement => int.Parse(statement.Split(':')[0].Split(',')[1], CultureInfo.InvariantCulture))
            .ToArray();

        Assert.Equal([375, 343, 311, 279, 247, 215, 183, 151, 119], positions);
        // The configured media is 50.13mm, which at 203 dpi is 401 dots.
        Assert.All(positions, y => Assert.InRange(y, 0, 400));
    }

    // A field below the bottom of the label is Error 1003 "Field out of label" and the printer discards the whole
    // label, so the lines that still fit must print - as legacy's DrawString clipped at the rectangle's edge.
    [Fact]
    public void ToFingerprint_MoreLinesThanTheLabelHolds_DropsTheLinesThatWouldFallOffIt()
    {
        var label = new AddressLabel([.. Enumerable.Range(1, 20).Select(line => $"Line {line}")]);

        var statements = Statements(label)[4..^3];

        Assert.Equal(12, statements.Length);
        Assert.Equal("PP 0,23:PT \"Line 12\"", statements[^1]);
    }

    [Fact]
    public void ToFingerprint_WritesADoubleQuoteAsAChrTerm()
    {
        var statements = Statements(new AddressLabel(["The \"Old\" Lab"]));

        Assert.Contains("PP 0,375:PT \"The \"+CHR$(34)+\"Old\"+CHR$(34)+\" Lab\"", statements);
    }

    [Fact]
    public void ToFingerprint_WritesLatin1CharactersAsChrTermsAndOtherCharactersAsAQuestionMark()
    {
        var statements = Statements(new AddressLabel(["Caf\u00E9 \u20AC"]));

        Assert.Contains("PP 0,375:PT \"Caf\"+CHR$(233)+\" ?\"", statements);
    }

    [Fact]
    public void ToFingerprint_ReplacesLineBreaksSoAnAddressLineCannotStartAStatement()
    {
        var statements = Statements(new AddressLabel(["A\r\nINPUT OFF\nB"]));

        Assert.Equal(8, statements.Length);
        Assert.Contains("PP 0,375:PT \"A  INPUT OFF B\"", statements);
    }

    [Fact]
    public void ToFingerprint_ColonInsideTheTextIsKeptInsideTheString()
    {
        var statements = Statements(new AddressLabel(["Tel: 01234"]));

        Assert.Contains("PP 0,375:PT \"Tel: 01234\"", statements);
    }

    [Fact]
    public void ToFingerprint_PayloadIsPureAscii()
    {
        var payload = FingerprintLabelFormatter.ToFingerprint(new AddressLabel(["Caf\u00E9 \u20AC \"x\""]));

        Assert.All(payload, b => Assert.True(b < 128));
    }

    [Fact]
    public void ToFingerprint_NullLabel_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FingerprintLabelFormatter.ToFingerprint(null!));
    }
}
