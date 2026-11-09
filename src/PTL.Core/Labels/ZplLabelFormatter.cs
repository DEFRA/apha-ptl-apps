using System.Globalization;
using System.Text;

namespace PTL.Core.Labels;

// Converts a composed AddressLabel into ZPL (Zebra Programming Language) commands - the design's
// "ZPL Utility: Address to ZPL" step. The PC43 (an Intermec/Honeywell PC43t, ZPL/EPL-capable) only
// renders a label correctly when sent ZPL markup over the raw/JetDirect port 9100 - plain text lines
// are not a valid print job for it. Layout mirrors AddressLabelPdfRenderer's 4in x 6in page.
public static class ZplLabelFormatter
{
    // PC43 default print density.
    private const int DotsPerInch = 203;
    private const int LabelWidthInches = 4;
    private const int LabelHeightInches = 6;
    private const int MarginDots = 40;
    private const int FontHeightDots = 30;
    private const int LineSpacingDots = 40;

    // The ZPL hex-escape indicator character (set by ^FH, default "_") - also escaped when it
    // appears literally in label text, so it is never misread as the start of an escape sequence.
    private const char HexEscapeIndicator = '_';

    public static byte[] ToZpl(AddressLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var zpl = new StringBuilder();
        zpl.Append("^XA\n");
        zpl.Append(string.Create(CultureInfo.InvariantCulture, $"^PW{LabelWidthInches * DotsPerInch}\n"));
        zpl.Append(string.Create(CultureInfo.InvariantCulture, $"^LL{LabelHeightInches * DotsPerInch}\n"));
        zpl.Append(string.Create(CultureInfo.InvariantCulture, $"^CF0,{FontHeightDots}\n"));

        var y = MarginDots;
        foreach (var line in label.Lines)
        {
            // ^FH tells the printer to treat the hex-escape indicator in the field data below as an
            // escape sequence, so EscapeZplFieldData's _XX sequences for ^, ~ and _ are decoded back
            // to literal characters instead of being misread as ZPL command syntax.
            zpl.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"^FO{MarginDots},{y}^FH^FD{EscapeZplFieldData(line)}^FS\n"));
            y += LineSpacingDots;
        }

        zpl.Append("^XZ\n");
        return Encoding.ASCII.GetBytes(zpl.ToString());
    }

    // ^ starts every ZPL command and ~ starts every ZPL control command, so either one appearing
    // literally in address text (e.g. an organisation name) would corrupt the label format unless
    // escaped. Non-ASCII characters are also escaped - ZPL field data is single-byte ASCII by
    // default, so anything outside that range would otherwise be sent as raw, unprintable bytes.
    private static string EscapeZplFieldData(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '^' or '~' or HexEscapeIndicator || character > 127)
            {
                escaped.Append(HexEscapeIndicator).Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                escaped.Append(character);
            }
        }

        return escaped.ToString();
    }
}
