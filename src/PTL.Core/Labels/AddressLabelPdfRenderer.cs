using System.Globalization;
using System.Text;

namespace PTL.Core.Labels;

// Replaces legacy LabelBase.Print(), which rendered the label with GDI+ and pushed it straight to
// a network printer from the web server - not possible outside Windows/on-premises hosting. The
// label is instead emitted as a single-page PDF for the user to download and print.
//
// Deliberately dependency-free: the output is 8-9 lines of plain text in one base-14 font, so a
// PDF library (and its licence obligations) would be disproportionate. Page size and font mirror
// the legacy label - 4in x 6in from LabelPrinterPrintableWidth/Height (400x600 hundredths of an
// inch), and Arial 10pt bold, for which Helvetica-Bold is the base-14 metric equivalent and so
// needs no font embedding.
public static class AddressLabelPdfRenderer
{
    public const string ContentType = "application/pdf";

    private const double PageWidthPoints = 288;
    private const double PageHeightPoints = 432;
    private const double MarginPoints = 18;
    private const double FontSizePoints = 10;
    private const double LeadingPoints = 12;

    public static byte[] Render(AddressLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return BuildPdf(BuildContentStream(label.Lines));
    }

    private static string BuildContentStream(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.Append("BT\n");
        content.Append(string.Create(CultureInfo.InvariantCulture, $"/F1 {FontSizePoints} Tf\n"));
        content.Append(string.Create(CultureInfo.InvariantCulture, $"{LeadingPoints} TL\n"));
        content.Append(string.Create(CultureInfo.InvariantCulture, $"{MarginPoints} {PageHeightPoints - MarginPoints - FontSizePoints} Td\n"));

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                content.Append("T*\n");
            }

            content.Append('(').Append(EscapeText(lines[i])).Append(") Tj\n");
        }

        content.Append("ET");
        return content.ToString();
    }

    // Parentheses and backslashes terminate or escape a PDF string literal, so user-supplied text
    // must be escaped to keep it data rather than content-stream operators. Control characters are
    // dropped for the same reason.
    private static string EscapeText(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '(':
                    escaped.Append("\\(");
                    break;
                case ')':
                    escaped.Append("\\)");
                    break;
                default:
                    escaped.Append(char.IsControl(character) ? ' ' : character);
                    break;
            }
        }

        return escaped.ToString();
    }

    private static byte[] BuildPdf(string contentStream)
    {
        // WinAnsiEncoding is declared on the font, and Latin1 is its byte-compatible subset.
        var encoding = Encoding.Latin1;
        var contentBytes = encoding.GetBytes(contentStream);

        byte[][] objects =
        [
            encoding.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            encoding.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            encoding.GetBytes(string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidthPoints} {PageHeightPoints}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>")),
            encoding.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
            [
                .. encoding.GetBytes(string.Create(CultureInfo.InvariantCulture, $"<< /Length {contentBytes.Length} >>\nstream\n")),
                .. contentBytes,
                .. encoding.GetBytes("\nendstream")
            ]
        ];

        using var buffer = new MemoryStream();
        Write(buffer, encoding, "%PDF-1.4\n");

        var offsets = new long[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = buffer.Position;
            Write(buffer, encoding, string.Create(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n"));
            buffer.Write(objects[i]);
            Write(buffer, encoding, "\nendobj\n");
        }

        var startXref = buffer.Position;
        Write(buffer, encoding, string.Create(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n"));
        Write(buffer, encoding, "0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write(buffer, encoding, offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        Write(buffer, encoding, string.Create(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF\n"));

        return buffer.ToArray();
    }

    private static void Write(Stream stream, Encoding encoding, string text) => stream.Write(encoding.GetBytes(text));
}
