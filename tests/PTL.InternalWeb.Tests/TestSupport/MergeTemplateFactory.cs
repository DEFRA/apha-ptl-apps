using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PTL.InternalWeb.Tests.TestSupport;

/// <summary>
/// Builds the kind of MERGEFIELD .docx an administrator uploads on the Exports screen, so tests can
/// exercise the real merge engine against bytes served from storage rather than a file on disk.
/// </summary>
public static class MergeTemplateFactory
{
    public static byte[] ContractTemplate(params string[] extraFields)
    {
        using var buffer = new MemoryStream();

        using (var document = WordprocessingDocument.Create(buffer, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();

            var heading = new Paragraph();
            heading.Append(MergeField("ContractNumber"));
            heading.Append(MergeField("CustomerOrganisation"));
            foreach (var field in extraFields)
            {
                heading.Append(MergeField(field));
            }

            var itemParagraph = new Paragraph();
            itemParagraph.Append(MergeField("TableStart:ContractItems"));
            itemParagraph.Append(MergeField("SchemeName"));
            itemParagraph.Append(MergeField("Price"));
            itemParagraph.Append(MergeField("TableEnd:ContractItems"));

            var itemRow = new TableRow(new TableCell(itemParagraph));
            var headerRow = new TableRow(new TableCell(new Paragraph(new Run(new Text("Scheme")))));

            mainPart.Document = new Document(new Body(heading, new Table(headerRow, itemRow)));
        }

        return buffer.ToArray();
    }

    private static OpenXmlElement[] MergeField(string name) =>
    [
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new FieldCode($" MERGEFIELD  {name}  \\* MERGEFORMAT ")),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        new Run(new Text($"\u00ab{name}\u00bb")),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End })
    ];
}
