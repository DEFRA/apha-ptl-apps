using PTL.Core.Contract.Document;

namespace PTL.Api.Tests.Contract;

public class FileTemplateRepositoryTests
{
    [Theory]
    [InlineData("Contract", "ContractExampleTemplate")]
    [InlineData("Address Confirmation", "AddressConfirmationExampleTemplate")]
    [InlineData("addressconfirmation", "AddressConfirmationExampleTemplate")]
    [InlineData("Job Sheet", "JobSheetExampleTemplate")]
    [InlineData("Renewal Letter", "ContractRenewalExampleTemplate")]
    public void TryResolve_MapsLegacyDocumentTypesToTemplateKeys(string documentType, string expectedKey)
    {
        Assert.True(ContractDocumentTypes.TryResolve(documentType, out _, out var templateKey));
        Assert.Equal(expectedKey, templateKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Import Permit")]
    public void TryResolve_RejectsUnknownDocumentTypes(string? documentType)
    {
        Assert.False(ContractDocumentTypes.TryResolve(documentType, out _, out _));
    }

    [Fact]
    public async Task GetAsync_ReturnsTheMappedDocxTemplate()
    {
        using var root = new TemplateRoot("ContractExampleTemplate.docx", "JobSheetExampleTemplate.docx");

        var template = await new FileTemplateRepository(root).GetAsync("Contract");

        Assert.NotNull(template);
        Assert.Equal("ContractExampleTemplate", template.TemplateKey);
        Assert.Equal("Contract", template.DocumentType);
    }

    [Fact]
    public async Task GetAsync_IgnoresLegacyDocTemplates()
    {
        using var root = new TemplateRoot("ContractExampleTemplate.doc");

        Assert.Null(await new FileTemplateRepository(root).GetAsync("Contract"));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenTheTemplateHasNotBeenCommitted()
    {
        using var root = new TemplateRoot("JobSheetExampleTemplate.docx");

        Assert.Null(await new FileTemplateRepository(root).GetAsync("Contract"));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForAnUnknownDocumentType()
    {
        using var root = new TemplateRoot("ContractExampleTemplate.docx");

        Assert.Null(await new FileTemplateRepository(root).GetAsync("Import Permit"));
    }

    [Fact]
    public async Task GetAsync_HonoursAnExplicitTemplateNameOverride()
    {
        using var root = new TemplateRoot("QM092Ed4Contract120110.docx");

        var template = await new FileTemplateRepository(root).GetAsync("Contract", "QM092Ed4Contract120110.docx");

        Assert.Equal("QM092Ed4Contract120110", template?.TemplateKey);
    }

    [Fact]
    public async Task GetByDocumentTypeAsync_ReturnsDatedVariantsOfTheMappedTemplate()
    {
        using var root = new TemplateRoot("ContractExampleTemplate.docx", "ContractExampleTemplate210212.docx", "JobSheetExampleTemplate.docx");

        var templates = await new FileTemplateRepository(root).GetByDocumentTypeAsync("Contract");

        Assert.Equal(2, templates.Count);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenTheTemplateFolderIsAbsent()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ptl-missing-{Guid.NewGuid():N}");

        Assert.Null(await new FileTemplateRepository(new FileTemplateLoader(missing)).GetAsync("Contract"));
    }

    private sealed class TemplateRoot : ITemplateLoader, IDisposable
    {
        private readonly string _path;

        public TemplateRoot(params string[] fileNames)
        {
            _path = Path.Combine(Path.GetTempPath(), $"ptl-templates-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_path);
            foreach (var fileName in fileNames)
            {
                File.WriteAllText(Path.Combine(_path, fileName), "template");
            }
        }

        public string GetTemplatesRoot() => _path;

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }
    }
}
