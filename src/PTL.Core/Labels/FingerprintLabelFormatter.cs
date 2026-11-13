using System.Globalization;
using System.Text;

namespace PTL.Core.Labels;

// Converts a composed AddressLabel into Intermec/Honeywell Fingerprint (Direct Protocol) statements, the command
// language the PC43t is configured with. The layout reproduces legacy LabelBase.Print(): Arial 10pt text drawn from
// the top-left of a 4in x 6in page (400 x 600 in 1/100 inch, from GetPrinterSettings). Fingerprint measures from the
// bottom-left with Y increasing upward, so each line's Y is converted from a distance down the page.
public static class FingerprintLabelFormatter
{
    private const int DotsPerInch = 203;

    // Legacy page height: 600 hundredths of an inch.
    private const int LabelHeightDots = 6 * DotsPerInch;

    // Legacy drew at X = 0.
    private const int LeftDots = 0;

    // GDI places Arial 10pt text with its baseline one ascent (0.905 em = 25.5 dots) below the top of the line,
    // and steps each line by Arial's line height (1.149 em = 32.4 dots).
    private const int FirstBaselineFromTopDots = 26;
    private const int LineSpacingDots = 32;

    private const string FontName = "Swiss 721 BT";
    private const int FontSizePoints = 10;
    private const string NewLine = "\r\n";

    public static byte[] ToFingerprint(AddressLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);

        // VERBON makes the printer answer each statement with Ok or an error message; LabelPrinterClient logs the reply.
        var fingerprint = new StringBuilder();
        fingerprint.Append("INPUT ON").Append(NewLine);
        fingerprint.Append("VERBON").Append(NewLine);
        fingerprint.Append("CLL").Append(NewLine);
        fingerprint.Append(CultureInfo.InvariantCulture, $"FT \"{FontName}\",{FontSizePoints}").Append(NewLine);

        var y = LabelHeightDots - FirstBaselineFromTopDots;
        foreach (var line in label.Lines)
        {
            fingerprint.Append(CultureInfo.InvariantCulture, $"PP {LeftDots},{y}:PT {ToStringExpression(line)}").Append(NewLine);
            y -= LineSpacingDots;
        }

        fingerprint.Append("PF").Append(NewLine);
        fingerprint.Append("VERBOFF").Append(NewLine);
        fingerprint.Append("INPUT OFF").Append(NewLine);
        return Encoding.ASCII.GetBytes(fingerprint.ToString());
    }

    // A Fingerprint string cannot contain a double quote or a line break, and the payload is sent as ASCII. So control
    // characters become spaces (an address line must never start a new statement), a double quote and Latin-1 characters
    // are written as CHR$(n) terms joined with "+", and anything beyond Latin-1 becomes "?".
    private static string ToStringExpression(string text)
    {
        var terms = new List<string>();
        var literal = new StringBuilder();

        foreach (var character in text)
        {
            if (character < ' ' || character == '\u007F')
            {
                literal.Append(' ');
            }
            else if (character == '"' || character is > '\u007F' and <= '\u00FF')
            {
                AddLiteral(terms, literal);
                terms.Add(string.Create(CultureInfo.InvariantCulture, $"CHR$({(int)character})"));
            }
            else if (character > '\u00FF')
            {
                literal.Append('?');
            }
            else
            {
                literal.Append(character);
            }
        }

        AddLiteral(terms, literal);
        return terms.Count == 0 ? "\"\"" : string.Join('+', terms);
    }

    private static void AddLiteral(List<string> terms, StringBuilder literal)
    {
        if (literal.Length == 0)
        {
            return;
        }

        terms.Add($"\"{literal}\"");
        literal.Clear();
    }
}
