using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PTL.Core.Contract.Export.Templates;

namespace PTL.Api.Tests.Contract;

public class ExportTemplateServiceTests
{
    private const string Docx = "Contract Template.docx";

    private static (ExportTemplateService Service, FakeUploadedTemplateRepository Repository, FakeTemplateStorageService Storage) CreateService(long maxUploadBytes = 4 * 1024 * 1024, string prefix = "templates")
    {
        var repository = new FakeUploadedTemplateRepository();
        var storage = new FakeTemplateStorageService();
        var options = Options.Create(new TemplateStorageOptions { Prefix = prefix, MaxUploadBytes = maxUploadBytes });

        return (new ExportTemplateService(repository, storage, options, NullLogger<ExportTemplateService>.Instance), repository, storage);
    }

    [Fact]
    public async Task UploadAsync_UnknownDocumentType_IsRejected()
    {
        var (service, repository, storage) = CreateService();

        var result = await service.UploadAsync("Not A Document Type", Docx, [1]);

        Assert.False(result.Success);
        Assert.Empty(repository.Templates);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task UploadAsync_EmptyContent_IsRejected()
    {
        var (service, repository, _) = CreateService();

        var result = await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, []);

        Assert.False(result.Success);
        Assert.Empty(repository.Templates);
    }

    // With no configured prefix the key starts at the document-type folder.
    [Fact]
    public async Task UploadAsync_NoConfiguredPrefix_OmitsItFromTheStorageKey()
    {
        var (service, repository, storage) = CreateService(prefix: string.Empty);

        await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1]);

        var fileId = repository.Templates[0].FileId;
        Assert.Contains($"contracts/{fileId}.docx", storage.Files.Keys);
    }

    [Fact]
    public async Task UploadAsync_AddressConfirmationLetters_UsesItsOwnFolder()
    {
        var (service, repository, storage) = CreateService();

        await service.UploadAsync(ExportDocumentTypes.AddressConfirmationLetters, "Address Confirmation.docx", [1]);

        var fileId = repository.Templates[0].FileId;
        Assert.Contains($"templates/address-confirmation/{fileId}.docx", storage.Files.Keys);
    }

    [Fact]
    public async Task UploadAsync_ValidDocx_StoresContentAndMetadata()
    {
        var (service, repository, storage) = CreateService();

        var result = await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1, 2, 3]);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Single(repository.Templates);
        Assert.Equal(Docx, repository.Templates[0].Filename);
        Assert.Equal(ExportDocumentTypes.Contracts, repository.Templates[0].DocumentType);

        // Newly uploaded templates are not active until the reviewer presses Select.
        Assert.False(repository.Templates[0].Selected);
        Assert.Single(storage.Files);
    }

    [Fact]
    public async Task UploadAsync_DerivesAStorageKeyFromTheDocumentTypeAndFileId()
    {
        var (service, repository, storage) = CreateService();

        await service.UploadAsync(ExportDocumentTypes.JobSheets, "Job Sheet.docx", [1]);

        var fileId = repository.Templates[0].FileId;
        Assert.Contains($"templates/job-sheets/{fileId}.docx", storage.Files.Keys);
    }

    [Theory]
    [InlineData("template.pdf")]
    [InlineData("template")]
    [InlineData("bad-name.docx")]
    [InlineData(" leading.docx")]
    public async Task UploadAsync_RejectsAnythingThatIsNotADocOrDocx(string fileName)
    {
        var (service, repository, _) = CreateService();

        var result = await service.UploadAsync(ExportDocumentTypes.Contracts, fileName, [1]);

        Assert.False(result.Success);
        Assert.Equal("The file does not have a .Doc or .Docx extension", result.ErrorMessage);
        Assert.Empty(repository.Templates);
    }

    [Fact]
    public async Task UploadAsync_EmptyContent_ReportsFileNotFound()
    {
        var (service, _, _) = CreateService();

        var result = await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, []);

        Assert.False(result.Success);
        Assert.Equal("File not found", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadAsync_OverTheSizeLimit_ReportsTheLegacyMessage()
    {
        var (service, _, _) = CreateService(maxUploadBytes: 2);

        var result = await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1, 2, 3]);

        Assert.False(result.Success);
        Assert.Equal("Maximum file size is 4MB", result.ErrorMessage);
    }

    // Legacy kept every template in one folder, so names collide across document types too.
    [Fact]
    public async Task UploadAsync_DuplicateNameOnAnotherDocumentType_IsRejected()
    {
        var (service, _, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1]);

        var result = await service.UploadAsync(ExportDocumentTypes.JobSheets, Docx, [1]);

        Assert.False(result.Success);
        Assert.Equal("A file with this name already exists", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadAsync_StorageFailure_DoesNotWriteMetadata()
    {
        var (service, repository, storage) = CreateService();
        storage.ThrowOnSave = true;

        var result = await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1]);

        Assert.False(result.Success);
        Assert.Equal("File could not be saved", result.ErrorMessage);
        Assert.Empty(repository.Templates);
    }

    [Fact]
    public async Task SelectAsync_ClearsThePreviouslySelectedTemplateOfThatType()
    {
        var (service, repository, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, "First.docx", [1]);
        await service.UploadAsync(ExportDocumentTypes.Contracts, "Second.docx", [1]);

        var first = repository.Templates[0].FileId;
        var second = repository.Templates[1].FileId;

        Assert.True(await service.SelectAsync(first));
        Assert.True(await service.SelectAsync(second));

        Assert.False(repository.Templates.Single(t => t.FileId == first).Selected);
        Assert.True(repository.Templates.Single(t => t.FileId == second).Selected);
    }

    [Fact]
    public async Task SelectAsync_LeavesAnotherDocumentTypesActiveTemplateAlone()
    {
        var (service, repository, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, "Contract.docx", [1]);
        await service.UploadAsync(ExportDocumentTypes.JobSheets, "JobSheet.docx", [1]);

        await service.SelectAsync(repository.Templates[0].FileId);
        await service.SelectAsync(repository.Templates[1].FileId);

        Assert.True(repository.Templates[0].Selected);
        Assert.True(repository.Templates[1].Selected);
    }

    [Fact]
    public async Task SelectAsync_UnknownFile_ReturnsFalse() =>
        Assert.False(await CreateService().Service.SelectAsync(Guid.NewGuid()));

    [Fact]
    public async Task GetActiveTemplateAsync_ReturnsTheSelectedTemplate()
    {
        var (service, repository, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1]);
        await service.SelectAsync(repository.Templates[0].FileId);

        var active = await service.GetActiveTemplateAsync(ExportDocumentTypes.Contracts);

        Assert.NotNull(active);
        Assert.Equal(Docx, active!.Filename);
    }

    [Fact]
    public async Task GetActiveTemplateAsync_NoneSelected_ReturnsNull()
    {
        var (service, _, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1]);

        Assert.Null(await service.GetActiveTemplateAsync(ExportDocumentTypes.Contracts));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsTheStoredBytesUnderTheOriginalFilename()
    {
        var (service, repository, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [7, 8, 9]);

        var content = await service.DownloadAsync(repository.Templates[0].FileId);

        Assert.NotNull(content);
        Assert.Equal(Docx, content!.FileName);
        Assert.Equal(ExportTemplateService.DocxContentType, content.ContentType);
        Assert.Equal<byte[]>([7, 8, 9], content.Content);
    }

    [Fact]
    public async Task DownloadAsync_UnknownFile_ReturnsNull() =>
        Assert.Null(await CreateService().Service.DownloadAsync(Guid.NewGuid()));

    [Fact]
    public async Task DeleteAsync_RemovesBothTheStoredFileAndTheMetadata()
    {
        var (service, repository, storage) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.Contracts, Docx, [1]);

        Assert.True(await service.DeleteAsync(repository.Templates[0].FileId));

        Assert.Empty(repository.Templates);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task DeleteAsync_UnknownFile_ReturnsFalse() =>
        Assert.False(await CreateService().Service.DeleteAsync(Guid.NewGuid()));

    // spgUploadedFilesByDocumentType does not select fldDocumentType back.
    [Fact]
    public async Task GetTemplatesAsync_BackfillsTheDocumentTypeTheProcedureOmits()
    {
        var (service, _, _) = CreateService();
        await service.UploadAsync(ExportDocumentTypes.RenewalLetters, "Renewal.docx", [1]);

        var templates = await service.GetTemplatesAsync(ExportDocumentTypes.RenewalLetters);

        Assert.Equal(ExportDocumentTypes.RenewalLetters, Assert.Single(templates).DocumentType);
    }

    [Fact]
    public async Task GetTemplatesAsync_UnknownDocumentType_ReturnsEmpty() =>
        Assert.Empty(await CreateService().Service.GetTemplatesAsync("Invoices"));
}
