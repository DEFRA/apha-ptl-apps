using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PTL.Contracts.Contract;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace PTL.Core.Contract.Document;

/// <summary>
/// Merges data into a <c>.docx</c> template by resolving Word <c>MERGEFIELD</c> fields in place,
/// reproducing the legacy Aspose.Words <c>MailMerge.Execute</c> / <c>ExecuteWithRegions</c> behaviour
/// without an Aspose dependency. Only field runs are rewritten, so images, styles, headers, footers
/// and section properties survive untouched and branding/layout are preserved.
/// </summary>
public sealed partial class TemplateMergeService : ITemplateMergeService
{
    private const string TableStartPrefix = "TableStart:";
    private const string TableEndPrefix = "TableEnd:";

    public byte[] MergeTemplate(
        string templatePath,
        IReadOnlyDictionary<string, string> mergeValues,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? regions = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(templatePath);
        if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Legacy template '{templatePath}' is {(string.IsNullOrEmpty(extension) ? "no extension" : extension)}. DOCX templates are required to preserve DOCX output; legacy .doc templates must be converted before merge.");
        }

        var stream = new MemoryStream();
        using (var source = File.OpenRead(templatePath))
        {
            source.CopyTo(stream);
        }

        stream.Position = 0;

        using (var document = WordprocessingDocument.Open(stream, isEditable: true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidDataException($"Template '{templatePath}' has no main document part.");

            var mainDocument = mainPart.Document
                ?? throw new InvalidDataException($"Template '{templatePath}' has no document content.");

            var body = mainDocument.Body
                ?? throw new InvalidDataException($"Template '{templatePath}' has no document body.");

            if (regions is not null)
            {
                foreach (var region in regions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ExpandRegion(body, region.Key, region.Value);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Headers and footers carry the APHA/VETQAS branding plus several address merge fields.
            MergeInto(mainDocument, mergeValues);
            foreach (var header in mainPart.HeaderParts)
            {
                var headerElement = header.Header
                    ?? throw new InvalidDataException($"Template '{templatePath}' has a header part with no header content.");
                MergeInto(headerElement, mergeValues);
            }

            foreach (var footer in mainPart.FooterParts)
            {
                var footerElement = footer.Footer
                    ?? throw new InvalidDataException($"Template '{templatePath}' has a footer part with no footer content.");
                MergeInto(footerElement, mergeValues);
            }

            mainDocument.Save();
        }

        return stream.ToArray();
    }

    public byte[] MergeTemplateMany(
        string templatePath,
        IReadOnlyList<ContractDocumentMergeData> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            return CreateEmptyDocument();
        }

        // One document is the overwhelmingly common case (Contract, Job Sheet, Renewal Letter) and
        // must stay byte-for-byte what a single merge produces - no concatenation wrapper.
        if (documents.Count == 1)
        {
            return MergeTemplate(templatePath, documents[0].MergeValues, documents[0].Regions, cancellationToken);
        }

        var first = MergeTemplate(templatePath, documents[0].MergeValues, documents[0].Regions, cancellationToken);

        var stream = new MemoryStream();
        stream.Write(first, 0, first.Length);
        stream.Position = 0;

        using (var document = WordprocessingDocument.Open(stream, isEditable: true))
        {
            var mainPart = document.MainDocumentPart!;
            var mainDocument = mainPart.Document
                ?? throw new InvalidDataException("Merged document has no document content.");
            var body = mainDocument.Body
                ?? throw new InvalidDataException("Merged document has no document body.");

            // Section properties must stay the last body element, so everything is inserted before it.
            var sectionProperties = body.Elements<SectionProperties>().LastOrDefault();

            for (var i = 1; i < documents.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var merged = MergeTemplate(templatePath, documents[i].MergeValues, documents[i].Regions, cancellationToken);

                var chunkPart = mainPart.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.WordprocessingML);
                using (var chunkStream = new MemoryStream(merged, writable: false))
                {
                    chunkPart.FeedData(chunkStream);
                }

                var pageBreakRun = new Run();
                pageBreakRun.AppendChild(new Break { Type = BreakValues.Page });
                var pageBreak = new Paragraph();
                pageBreak.AppendChild(pageBreakRun);
                var altChunk = new AltChunk { Id = mainPart.GetIdOfPart(chunkPart) };

                if (sectionProperties is null)
                {
                    body.AppendChild(pageBreak);
                    body.AppendChild(altChunk);
                }
                else
                {
                    body.InsertBefore(pageBreak, sectionProperties);
                    body.InsertBefore(altChunk, sectionProperties);
                }
            }

            mainDocument.Save();
        }

        return stream.ToArray();
    }

    // Legacy MailMerge.CreateDocument() clears all sections, so an export with no data rows writes
    // an empty document rather than a letter full of blanks.
    private static byte[] CreateEmptyDocument()
    {
        var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            var mainDocument = new WordDocument();
            mainDocument.AppendChild(new Body());
            mainPart.Document = mainDocument;
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static void MergeInto(OpenXmlElement root, IReadOnlyDictionary<string, string> values)
    {
        ResolveSimpleFields(root, values);
        ResolveComplexFields(root, values);
        ReplacePlainTokens(root, values);
    }

    // <w:fldSimple w:instr=" MERGEFIELD Name "> form.
    private static void ResolveSimpleFields(OpenXmlElement root, IReadOnlyDictionary<string, string> values)
    {
        foreach (var field in root.Descendants<SimpleField>().ToList())
        {
            if (!TryGetMergeFieldName(field.Instruction?.Value, out var name))
            {
                continue;
            }

            var replacement = BuildValueRun(values, name, field.Descendants<Run>().FirstOrDefault());
            field.Parent?.ReplaceChild(replacement, field);
        }
    }

    // fldChar begin / instrText / separate / result / fldChar end form.
    private static void ResolveComplexFields(OpenXmlElement root, IReadOnlyDictionary<string, string> values)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            foreach (var span in FindFieldSpans(paragraph))
            {
                var instruction = string.Concat(span.SelectMany(e => e.Descendants<FieldCode>()).Select(f => f.Text));
                if (!TryGetMergeFieldName(instruction, out var name))
                {
                    continue;
                }

                // The span elements are themselves runs, which Descendants<Run>() would exclude.
                var formatSource = span
                    .SelectMany(SelfAndDescendants)
                    .OfType<Run>()
                    .FirstOrDefault(r => r.Elements<Text>().Any());

                var replacement = BuildValueRun(values, name, formatSource);
                paragraph.InsertBefore<Run>(replacement, span[0]);
                foreach (var element in span)
                {
                    element.Remove();
                }
            }
        }
    }

    // Captures element references rather than indices so the caller can mutate the paragraph safely.
    private static List<List<OpenXmlElement>> FindFieldSpans(Paragraph paragraph)
    {
        var spans = new List<List<OpenXmlElement>>();
        var children = paragraph.ChildElements.ToList();
        var depth = 0;
        var begin = -1;

        for (var i = 0; i < children.Count; i++)
        {
            foreach (var fieldChar in children[i].Descendants<FieldChar>())
            {
                TrackFieldCharBoundary(fieldChar, i, children, spans, ref depth, ref begin);
            }
        }

        return spans;
    }

    // Tracks nested field begin/end markers so only a fully-closed top-level span is captured.
    private static void TrackFieldCharBoundary(
        FieldChar fieldChar,
        int index,
        List<OpenXmlElement> children,
        List<List<OpenXmlElement>> spans,
        ref int depth,
        ref int begin)
    {
        var type = fieldChar.FieldCharType?.InnerText;
        if (string.Equals(type, "begin", StringComparison.OrdinalIgnoreCase))
        {
            if (depth == 0)
            {
                begin = index;
            }

            depth++;
            return;
        }

        if (!string.Equals(type, "end", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        depth = Math.Max(0, depth - 1);
        if (depth == 0 && begin >= 0)
        {
            spans.Add(children.GetRange(begin, index - begin + 1));
            begin = -1;
        }
    }

    // Compatibility shim for templates authored with {{Token}} placeholders rather than merge fields.
    private static void ReplacePlainTokens(OpenXmlElement root, IReadOnlyDictionary<string, string> values)
    {
        foreach (var text in root.Descendants<Text>().Where(t => t.Text.Contains("{{", StringComparison.Ordinal)))
        {
            text.Text = ApplyTokens(text.Text, values);
        }

        // A token Word has split across runs only resolves once those runs are joined.
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count < 2)
            {
                continue;
            }

            var combined = string.Concat(texts.Select(t => t.Text));
            if (!combined.Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            var merged = ApplyTokens(combined, values);
            if (merged == combined)
            {
                continue;
            }

            texts[0].Text = merged;
            texts[0].Space = SpaceProcessingModeValues.Preserve;
            for (var i = 1; i < texts.Count; i++)
            {
                texts[i].Text = string.Empty;
            }
        }
    }

    private static string ApplyTokens(string input, IReadOnlyDictionary<string, string> values) =>
        PlainTokenPattern().Replace(input, match =>
            values.TryGetValue(match.Groups[1].Value.Trim(), out var value) ? value : match.Value);

    // Repeats the rows between TableStart:{region} and TableEnd:{region} once per data row.
    private static void ExpandRegion(
        OpenXmlElement body,
        string regionName,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var startRow = FindRowContainingField(body, TableStartPrefix + regionName);
        var endRow = FindRowContainingField(body, TableEndPrefix + regionName);
        if (startRow is null || endRow is null || startRow.Parent is null || !ReferenceEquals(startRow.Parent, endRow.Parent))
        {
            return;
        }

        var table = startRow.Parent;
        var allRows = table.Elements<TableRow>().ToList();
        var startIndex = allRows.IndexOf(startRow);
        var endIndex = allRows.IndexOf(endRow);
        if (startIndex < 0 || endIndex < startIndex)
        {
            return;
        }

        var templateRows = allRows.GetRange(startIndex, endIndex - startIndex + 1);

        foreach (var row in rows)
        {
            foreach (var templateRow in templateRows)
            {
                var clone = (TableRow)templateRow.CloneNode(true);
                MergeInto(clone, row);
                table.InsertBefore(clone, templateRows[0]);
            }
        }

        // Aspose removes the region markers, and the whole block when there is no data.
        foreach (var templateRow in templateRows)
        {
            templateRow.Remove();
        }
    }

    private static TableRow? FindRowContainingField(OpenXmlElement body, string fieldName)
    {
        foreach (var row in body.Descendants<TableRow>())
        {
            foreach (var simple in row.Descendants<SimpleField>())
            {
                if (TryGetMergeFieldName(simple.Instruction?.Value, out var name)
                    && string.Equals(name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }

            var instructions = string.Concat(row.Descendants<FieldCode>().Select(f => f.Text));
            if (instructions.Contains(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    // An unmatched merge field is blanked, matching legacy MailMergeCleanupOptions.RemoveUnusedFields.
    private static Run BuildValueRun(IReadOnlyDictionary<string, string> values, string name, Run? formatSource)
    {
        var text = values.TryGetValue(name, out var value) ? value : string.Empty;
        var run = new Run();

        if (formatSource?.RunProperties is not null)
        {
            run.RunProperties = (RunProperties)formatSource.RunProperties.CloneNode(true);
        }

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static IEnumerable<OpenXmlElement> SelfAndDescendants(OpenXmlElement element) =>
        Enumerable.Repeat(element, 1).Concat(element.Descendants());

    private static bool TryGetMergeFieldName(string? instruction, out string name)
    {
        name = string.Empty;
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return false;
        }

        var match = MergeFieldPattern().Match(instruction);
        if (!match.Success)
        {
            return false;
        }

        name = match.Groups[1].Value;
        return true;
    }

    [GeneratedRegex(@"^\s*MERGEFIELD\s+""?([^""\\\s]+)""?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MergeFieldPattern();

    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlainTokenPattern();
}
