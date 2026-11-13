using System.Text;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Labels;

public class FingerprintLabelFormatterTests
{
    private static string[] Statements(AddressLabel label) =>
        Encoding.ASCII.GetString(FingerprintLabelFormatter.ToFingerprint(label)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void ToFingerprint_EnablesDirectProtocolClearsTheLayoutSelectsTheFontPrintsAndSwitchesDirectProtocolOff()
    {
        var statements = Statements(AddressLabel.FromFields("Alice Example"));

        Assert.Equal(["INPUT ON", "CLL", "FT \"Swiss 721 BT\",10"], statements[..3]);
        Assert.Equal(["PF", "INPUT OFF"], statements[^2..]);
    }

    [Fact]
    public void ToFingerprint_EmitsOnePositionAndTextStatementPerLine_StartingAtTheTopAndStepsDown()
    {
        var statements = Statements(AddressLabel.FromFields("Alice Example", "1 Sample Street", "United Kingdom"));

        Assert.Equal(
            ["PP 40,1148:PT \"Alice Example\"", "PP 40,1108:PT \"1 Sample Street\"", "PP 40,1068:PT \"United Kingdom\""],
            statements[3..^2]);
    }

    [Fact]
    public void ToFingerprint_WritesADoubleQuoteAsAChrTerm()
    {
        var statements = Statements(new AddressLabel(["The \"Old\" Lab"]));

        Assert.Contains("PP 40,1148:PT \"The \"+CHR$(34)+\"Old\"+CHR$(34)+\" Lab\"", statements);
    }

    [Fact]
    public void ToFingerprint_WritesLatin1CharactersAsChrTermsAndOtherCharactersAsAQuestionMark()
    {
        var statements = Statements(new AddressLabel(["Caf\u00E9 \u20AC"]));

        Assert.Contains("PP 40,1148:PT \"Caf\"+CHR$(233)+\" ?\"", statements);
    }

    [Fact]
    public void ToFingerprint_ReplacesLineBreaksSoAnAddressLineCannotStartAStatement()
    {
        var statements = Statements(new AddressLabel(["A\r\nINPUT OFF\nB"]));

        Assert.Equal(6, statements.Length);
        Assert.Contains("PP 40,1148:PT \"A  INPUT OFF B\"", statements);
    }

    [Fact]
    public void ToFingerprint_ColonInsideTheTextIsKeptInsideTheString()
    {
        var statements = Statements(new AddressLabel(["Tel: 01234"]));

        Assert.Contains("PP 40,1148:PT \"Tel: 01234\"", statements);
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
