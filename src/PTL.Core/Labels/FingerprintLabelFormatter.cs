using System.Globalization;
using System.Text;

namespace PTL.Core.Labels;

// Converts a composed AddressLabel into Intermec/Honeywell Fingerprint (Direct Protocol) statements, the command
// language the PC43t is configured with. Unlike ZPL, Fingerprint's origin is the bottom-left of the label and Y
// increases upward, so the first line is placed near the top and each later line steps down. The 4in x 6in label at
// 203 dpi is an assumption carried over from the previous layout - the media size configured on the printer must match.
public static class FingerprintLabelFormatter
{
    private const int DotsPerInch = 203;
    private const int LabelHeightInches = 6;
    private const int MarginDots = 40;
    private const int FontHeightDots = 30;
    private const int LineSpacingDots = 40;
    private const string FontName = "Swiss 721 BT";
    private const int FontSizePoints = 10;
    private const string NewLine = "\r\n";

    public static byte[] ToFingerprint(AddressLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var fingerprint = new StringBuilder();
        fingerprint.Append("INPUT ON").Append(NewLine);
        fingerprint.Append("CLL").Append(NewLine);
        fingerprint.Append(CultureInfo.InvariantCulture, $"FT \"{FontName}\",{FontSizePoints}").Append(NewLine);

        var y = (LabelHeightInches * DotsPerInch) - MarginDots - FontHeightDots;
        foreach (var line in label.Lines)
        {
            fingerprint.Append(CultureInfo.InvariantCulture, $"PP {MarginDots},{y}:PT {ToStringExpression(line)}").Append(NewLine);
            y -= LineSpacingDots;
        }

        fingerprint.Append("PF").Append(NewLine);
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
