using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.Api.Tests.Contract;

public class TemplateMergeServiceTests
{
    [Fact]
    public void MergeTemplate_ResolvesComplexMergeFields()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(Para(
            new Run(new Text("Contract ")),
            MergeField("ContractNumber"),
            new Run(new Text(" for ")),
            MergeField("CustomerOrganisation"))));

        var text = MergedText(fixture.Path, new()
        {
            ["ContractNumber"] = "UT3/306",
            ["CustomerOrganisation"] = "Sample Laboratories Ltd"
        });

        Assert.Equal("Contract UT3/306 for Sample Laboratories Ltd", text);
    }

    [Fact]
    public void MergeTemplate_ResolvesSimpleMergeFields()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(new Paragraph(
            new SimpleField(new Run(new Text("\u00abContractNumber\u00bb"))) { Instruction = " MERGEFIELD ContractNumber \\* MERGEFORMAT " })));

        Assert.Equal("UT3/306", MergedText(fixture.Path, new() { ["ContractNumber"] = "UT3/306" }));
    }

    [Fact]
    public void MergeTemplate_BlanksUnsuppliedMergeFields()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(Para(
            new Run(new Text("Fax: ")),
            MergeField("Fax"))));

        Assert.Equal("Fax: ", MergedText(fixture.Path, []));
    }

    [Fact]
    public void MergeTemplate_PreservesRunFormattingOfTheReplacedField()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(Para(
            MergeField("ContractNumber", new RunProperties(new Bold())))));

        var bytes = new TemplateMergeService().MergeTemplate(fixture.Path, new Dictionary<string, string> { ["ContractNumber"] = "UT3/306" });

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var run = document.MainDocumentPart!.Document!.Body!.Descendants<Run>().Single();
        Assert.Equal("UT3/306", run.InnerText);
        Assert.NotNull(run.RunProperties?.Bold);
    }

    [Fact]
    public void MergeTemplate_MergesHeadersAndFooters()
    {
        using var fixture = new TemplateFixture(
            body => body.AppendChild(new Paragraph(new Run(new Text("body")))),
            withHeaderFooterField: "CustomerOrganisation");

        var bytes = new TemplateMergeService().MergeTemplate(fixture.Path, new Dictionary<string, string> { ["CustomerOrganisation"] = "Sample Labs" });

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var main = document.MainDocumentPart!;
        Assert.Equal("Sample Labs", main.HeaderParts.Single().Header.InnerText);
        Assert.Equal("Sample Labs", main.FooterParts.Single().Footer.InnerText);
    }

    [Fact]
    public void MergeTemplate_RepeatsRegionRowsPerDataRow()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(new Table(
            new TableRow(new TableCell(new Paragraph(new Run(new Text("Scheme"))))),
            new TableRow(
                Cell(MergeField("TableStart:ContractItems"), MergeField("SchemeName")),
                Cell(MergeField("Price"), MergeField("TableEnd:ContractItems"))))));

        var bytes = new TemplateMergeService().MergeTemplate(
            fixture.Path,
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["ContractItems"] =
                [
                    new Dictionary<string, string> { ["SchemeName"] = "Salmonella", ["Price"] = "\u00a342.50" },
                    new Dictionary<string, string> { ["SchemeName"] = "Campylobacter", ["Price"] = "\u00a318.00" }
                ]
            });

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var rows = document.MainDocumentPart!.Document!.Body!.Descendants<TableRow>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Salmonella\u00a342.50", rows[1].InnerText);
        Assert.Equal("Campylobacter\u00a318.00", rows[2].InnerText);
    }

    [Fact]
    public void MergeTemplate_RemovesRegionBlockWhenThereIsNoData()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(new Table(
            new TableRow(new TableCell(new Paragraph(new Run(new Text("Scheme"))))),
            new TableRow(Cell(
                MergeField("TableStart:ContractItems"),
                MergeField("SchemeName"),
                MergeField("TableEnd:ContractItems"))))));

        var bytes = new TemplateMergeService().MergeTemplate(
            fixture.Path,
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> { ["ContractItems"] = [] });

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Single(document.MainDocumentPart!.Document!.Body!.Descendants<TableRow>());
    }

    [Fact]
    public void MergeTemplate_ExpandsTwoNamedRegionsIndependently()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(new Table(
            new TableRow(Cell(
                MergeField("TableStart:FeePayingSchemes"),
                MergeField("SchemeName"),
                MergeField("TableEnd:FeePayingSchemes"))),
            new TableRow(Cell(
                MergeField("TableStart:NonFeePayingSchemes"),
                MergeField("SchemeName"),
                MergeField("TableEnd:NonFeePayingSchemes"))))));

        var bytes = new TemplateMergeService().MergeTemplate(
            fixture.Path,
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["FeePayingSchemes"] =
                [
                    new Dictionary<string, string> { ["SchemeName"] = "Salmonella" },
                    new Dictionary<string, string> { ["SchemeName"] = "Campylobacter" }
                ],
                ["NonFeePayingSchemes"] =
                [
                    new Dictionary<string, string> { ["SchemeName"] = "Listeria" }
                ]
            });

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var rows = document.MainDocumentPart!.Document!.Body!.Descendants<TableRow>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Salmonella", rows[0].InnerText);
        Assert.Equal("Campylobacter", rows[1].InnerText);
        Assert.Equal("Listeria", rows[2].InnerText);
    }

    [Fact]
    public void MergeTemplate_RegionNameNotFoundInTemplate_LeavesTemplateUnchanged()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(Para(new Run(new Text("No regions here")))));

        var bytes = new TemplateMergeService().MergeTemplate(
            fixture.Path,
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["UnknownRegion"] = [new Dictionary<string, string> { ["SchemeName"] = "Salmonella" }]
            });

        Assert.Equal("No regions here", InnerText(bytes));
    }

    [Fact]
    public void MergeTemplateMany_WithNoDocuments_ReturnsEmptyDocument()
    {
        var bytes = new TemplateMergeService().MergeTemplateMany("unused.docx", []);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Empty(document.MainDocumentPart!.Document!.Body!.ChildElements);
    }

    [Fact]
    public void MergeTemplateMany_WithSingleDocument_ProducesSameOutputAsSingleMerge()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(Para(MergeField("QalNumber"))));
        var values = new Dictionary<string, string> { ["QalNumber"] = "QAL/00001" };
        var service = new TemplateMergeService();

        var many = service.MergeTemplateMany(fixture.Path, [new ContractDocumentMergeData(values, null)]);

        Assert.Equal("QAL/00001", InnerText(many));
        using var stream = new MemoryStream(many);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Empty(document.MainDocumentPart!.AlternativeFormatImportParts);
    }

    [Fact]
    public void MergeTemplateMany_AppendsOneChunkPerDocument()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(Para(MergeField("QalNumber"))));

        var bytes = new TemplateMergeService().MergeTemplateMany(
            fixture.Path,
            [
                new ContractDocumentMergeData(new Dictionary<string, string> { ["QalNumber"] = "QAL/00001" }, null),
                new ContractDocumentMergeData(new Dictionary<string, string> { ["QalNumber"] = "QAL/00002" }, null),
                new ContractDocumentMergeData(new Dictionary<string, string> { ["QalNumber"] = "QAL/00003" }, null)
            ]);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);

        // The first letter is the host document; each subsequent letter is embedded as an AltChunk
        // which Word resolves on open.
        Assert.Equal("QAL/00001", document.MainDocumentPart!.Document!.Body!.Descendants<Text>().First().Text);
        Assert.Equal(2, document.MainDocumentPart.AlternativeFormatImportParts.Count());
        Assert.Equal(2, document.MainDocumentPart.Document!.Body!.Descendants<AltChunk>().Count());
    }

    [Fact]
    public void MergeTemplate_LeavesNonMergeFieldsAlone()
    {
        using var fixture = new TemplateFixture(body => body.AppendChild(new Paragraph(
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGE ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("1")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }))));

        var bytes = new TemplateMergeService().MergeTemplate(fixture.Path, new Dictionary<string, string>());

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Contains(
            document.MainDocumentPart!.Document!.Body!.Descendants<FieldCode>(),
            f => f.Text.Contains("PAGE", StringComparison.Ordinal));
    }

    [Fact]
    public void MergeTemplate_ThrowsForLegacyDocTemplateToPreserveDocxOutputContract()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ptl-legacy-{Guid.NewGuid():N}.doc");
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);

        try
        {
            var ex = Assert.Throws<NotSupportedException>(() =>
                new TemplateMergeService().MergeTemplate(path, new Dictionary<string, string> { ["ContractNumber"] = "UT3/306" }));

            Assert.Contains("DOCX templates are required", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string MergedText(string templatePath, Dictionary<string, string> values) =>
        InnerText(new TemplateMergeService().MergeTemplate(templatePath, values));

    private static string InnerText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }

    // The five-run fldChar form Word writes for a real mail-merge field.
    internal static OpenXmlElement[] MergeFieldElements(string name, RunProperties? runProperties = null)
    {
        // rPr must be the first child of w:r or Word drops it on reload.
        var resultRun = runProperties is null
            ? new Run(new Text($"\u00ab{name}\u00bb"))
            : new Run(runProperties, new Text($"\u00ab{name}\u00bb"));
        return
        [
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode($" MERGEFIELD  {name}  \\* MERGEFORMAT ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            resultRun,
            new Run(new FieldChar { FieldCharType = FieldCharValues.End })
        ];
    }

    private static object[] MergeField(string name, RunProperties? runProperties = null) =>
        [MergeFieldElements(name, runProperties)];

    private static Paragraph Para(params object[] parts)
    {
        var paragraph = new Paragraph();
        Append(paragraph, parts);
        return paragraph;
    }

    private static TableCell Cell(params object[] parts) => new(Para(parts));

    private static void Append(OpenXmlElement target, IEnumerable<object> parts)
    {
        foreach (var part in parts)
        {
            switch (part)
            {
                case OpenXmlElement element:
                    target.AppendChild(element);
                    break;
                case IEnumerable<object> nested:
                    Append(target, nested);
                    break;
            }
        }
    }

    internal sealed class TemplateFixture : IDisposable
    {
        public TemplateFixture(Action<Body> build, string? withHeaderFooterField = null)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ptl-template-{Guid.NewGuid():N}.docx");

            using var document = WordprocessingDocument.Create(Path, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            var body = new Body();
            build(body);
            mainPart.Document = new Document(body);

            if (withHeaderFooterField is null)
            {
                return;
            }

            var headerParagraph = new Paragraph();
            headerParagraph.Append(MergeFieldElements(withHeaderFooterField));
            mainPart.AddNewPart<HeaderPart>().Header = new Header(headerParagraph);

            var footerParagraph = new Paragraph();
            footerParagraph.Append(MergeFieldElements(withHeaderFooterField));
            mainPart.AddNewPart<FooterPart>().Footer = new Footer(footerParagraph);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
