using System.Globalization;
using System.Text;

namespace PTL.Core.Labels;

// Converts a composed AddressLabel into Intermec/Honeywell Fingerprint (Direct Protocol) statements, the command
// language the PC43d is configured with. The layout reproduces legacy LabelBase.Print(): 10pt bold text drawn from
// the top-left of the label, one line per address field. Fingerprint measures from the bottom-left of the label with
// Y increasing upward, so each line's Y is converted from its distance down the label.
public static class FingerprintLabelFormatter
{
    // The label the printer is set up for, in dots. Legacy's LabelPrinterPrintableWidth/Height (400 x 600 hundredths
    // of an inch) described the Windows driver's 4in x 6in page instead, and the driver scaled that page onto whatever
    // media was loaded; Fingerprint places each field on the media itself, so these must be the media's own size or
    // the text lands off the label and it feeds out blank. The PC43d is configured for 75mm x 50.13mm media and its
    // printhead is 203 dpi (203 / 25.4 = 8 dots per mm), so the label is 75 x 8 = 600 by 50.13 x 8 = 401 dots.
    private const int LabelHeightDots = 401;

    // Legacy drew at X = 0.
    private const int LeftDots = 0;

    // GDI placed Arial 10pt text with its baseline one ascent (0.905 em = 25.5 dots) below the top of the line, and
    // stepped each line by Arial's line height (1.149 em = 32.4 dots).
    private const int FirstBaselineFromTopDots = 26;
    private const int LineSpacingDots = 32;

    // Resident PC43d font closest to legacy's Arial 10pt Bold.
    private const string FontName = "Univers Bold";
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
            // A field below the bottom of the label is Error 1003 "Field out of label" and costs the whole label, so
            // lines that no longer fit are dropped - legacy's DrawString likewise clipped them at the rectangle's edge.
            if (y < 0)
            {
                break;
            }

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
