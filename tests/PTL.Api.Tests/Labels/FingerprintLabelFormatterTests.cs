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

        Assert.Equal(["INPUT ON", "VERBON", "CLL", "FT \"Swiss 721 BT\",10"], statements[..4]);
        Assert.Equal(["PF", "VERBOFF", "INPUT OFF"], statements[^3..]);
    }

    // Legacy: Arial 10pt from the top-left of a 4in x 6in (1218 dot) page, one Arial line height (32 dots) per line.
    [Fact]
    public void ToFingerprint_EmitsOnePositionAndTextStatementPerLine_StartingAtTheTopLeftAndStepsDown()
    {
        var statements = Statements(AddressLabel.FromFields("Alice Example", "1 Sample Street", "United Kingdom"));

        Assert.Equal(
            ["PP 0,1192:PT \"Alice Example\"", "PP 0,1160:PT \"1 Sample Street\"", "PP 0,1128:PT \"United Kingdom\""],
            statements[4..^3]);
    }

    [Fact]
    public void ToFingerprint_WritesADoubleQuoteAsAChrTerm()
    {
        var statements = Statements(new AddressLabel(["The \"Old\" Lab"]));

        Assert.Contains("PP 0,1192:PT \"The \"+CHR$(34)+\"Old\"+CHR$(34)+\" Lab\"", statements);
    }

    [Fact]
    public void ToFingerprint_WritesLatin1CharactersAsChrTermsAndOtherCharactersAsAQuestionMark()
    {
        var statements = Statements(new AddressLabel(["Caf\u00E9 \u20AC"]));

        Assert.Contains("PP 0,1192:PT \"Caf\"+CHR$(233)+\" ?\"", statements);
    }

    [Fact]
    public void ToFingerprint_ReplacesLineBreaksSoAnAddressLineCannotStartAStatement()
    {
        var statements = Statements(new AddressLabel(["A\r\nINPUT OFF\nB"]));

        Assert.Equal(8, statements.Length);
        Assert.Contains("PP 0,1192:PT \"A  INPUT OFF B\"", statements);
    }

    [Fact]
    public void ToFingerprint_ColonInsideTheTextIsKeptInsideTheString()
    {
        var statements = Statements(new AddressLabel(["Tel: 01234"]));

        Assert.Contains("PP 0,1192:PT \"Tel: 01234\"", statements);
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
