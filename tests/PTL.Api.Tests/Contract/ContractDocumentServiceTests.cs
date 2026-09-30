using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.Api.Tests.Contract;

public class ContractDocumentServiceTests
{
    [Fact]
    public async Task GenerateAsync_ReplacesMergeTokensInDocxTemplate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ptl-doc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var templatePath = Path.Combine(tempDir, "contract-template.docx");
            using (var templateDocument = WordprocessingDocument.Create(templatePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = templateDocument.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Contract {{ContractNumber}} for {{CustomerName}}")))));
            }

            var service = new ContractDocumentService(
                new FakeTemplateRepository(new DocumentTemplate("contract-template", "Contract", "Contract", templatePath)),
                new TemplateMergeService());

            var result = await service.GenerateAsync(new ContractDocumentRequest(
                "Contract",
                "Contract",
                "contract-output.docx",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ContractNumber"] = "UT-0001",
                    ["CustomerName"] = "Sample Laboratories Ltd"
                }));

            using var stream = new MemoryStream(result.DocumentBytes);
            using var doc = WordprocessingDocument.Open(stream, false);
            var text = doc.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
            Assert.Contains("UT-0001", text);
            Assert.Contains("Sample Laboratories Ltd", text);
            Assert.DoesNotContain("{{ContractNumber}}", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("{{CustomerName}}", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenTemplateDoesNotExist()
    {
        var service = new ContractDocumentService(
            new FakeTemplateRepository(),
            new TemplateMergeService());

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.GenerateAsync(new ContractDocumentRequest("Contract", "MissingTemplate", "contract.docx", new Dictionary<string, string>())));
    }

    [Fact]
    public void MergeTemplate_ThrowsForLegacyDocTemplateToPreserveDocxOutputContract()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ptl-doc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var templatePath = Path.Combine(tempDir, "legacy-contract-template.doc");
            File.WriteAllBytes(templatePath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            var ex = Assert.Throws<NotSupportedException>(() =>
                new TemplateMergeService().MergeTemplate(templatePath, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ContractNumber"] = "UT-0001"
                }));

            Assert.Contains("DOCX templates are required", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_PassesRegionsThroughToTheMergeService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ptl-doc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var templatePath = Path.Combine(tempDir, "contract-template.docx");
            using (var templateDocument = WordprocessingDocument.Create(templatePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = templateDocument.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("body")))));
            }

            var mergeService = new RecordingMergeService();
            var service = new ContractDocumentService(
                new FakeTemplateRepository(new DocumentTemplate("contract-template", "Contract", "Contract", templatePath)),
                mergeService);

            var regions = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["ContractItems"] = [new Dictionary<string, string> { ["SchemeName"] = "Salmonella" }]
            };

            var result = await service.GenerateAsync(new ContractDocumentRequest(
                "Contract", "Contract", "contract-output.docx", new Dictionary<string, string>(), regions));

            Assert.Equal("contract-output.docx", result.FileName);
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
            Assert.Same(regions, mergeService.LastRegions);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private sealed class RecordingMergeService : ITemplateMergeService
    {
        public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? LastRegions { get; private set; }

        public byte[] MergeTemplate(
            string templatePath,
            IReadOnlyDictionary<string, string> mergeValues,
            IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? regions = null,
            CancellationToken cancellationToken = default)
        {
            LastRegions = regions;
            return [0x50, 0x4B];
        }

        public byte[] MergeTemplateMany(
            string templatePath,
            IReadOnlyList<ContractDocumentMergeData> documents,
            CancellationToken cancellationToken = default)
        {
            LastRegions = documents.Count > 0 ? documents[0].Regions : null;
            return [0x50, 0x4B];
        }
    }

    private sealed class FakeTemplateRepository(DocumentTemplate? template = null) : ITemplateRepository
    {
        public Task<DocumentTemplate?> GetAsync(string documentType, string? templateName = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(template);

        public Task<IReadOnlyList<DocumentTemplate>> GetByDocumentTypeAsync(string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemplate>>(template is null ? [] : [template]);
    }
}
