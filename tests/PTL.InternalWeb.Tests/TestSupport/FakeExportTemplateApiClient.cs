using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Tests.TestSupport;

public sealed class FakeExportTemplateApiClient : IExportTemplateApiClient
{
    public ExportTemplateListResponse? Templates { get; set; }

    public ExportTemplateUploadResponse UploadResponse { get; set; } = new(true, null, null);

    public ExportTemplateDownload? Download { get; set; }

    public bool SelectResult { get; set; } = true;

    public bool DeleteResult { get; set; } = true;

    public string? LastUploadedFileName { get; private set; }

    public Guid? LastDownloadedFileId { get; private set; }

    public Guid? LastSelectedFileId { get; private set; }

    public Guid? LastDeletedFileId { get; private set; }

    /// <summary>Seeds a document type with several templates, exactly one of them selected.</summary>
    public FakeExportTemplateApiClient WithSelectedTemplate(string documentType, Guid selectedFileId, string fileName, byte[] content)
    {
        Templates = new ExportTemplateListResponse(documentType, documentType,
        [
            new ExportTemplateResponse(Guid.NewGuid(), "TemplateA.docx", DateTime.UtcNow.AddDays(-2), documentType, false),
            new ExportTemplateResponse(selectedFileId, fileName, DateTime.UtcNow.AddDays(-1), documentType, true),
            new ExportTemplateResponse(Guid.NewGuid(), "TemplateC.docx", DateTime.UtcNow, documentType, false)
        ]);

        Download = new ExportTemplateDownload(fileName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", content);
        return this;
    }

    public Task<ExportTemplateListResponse?> GetTemplatesAsync(string documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates);

    public Task<ExportTemplateUploadResponse> UploadTemplateAsync(string documentType, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        LastUploadedFileName = fileName;
        return Task.FromResult(UploadResponse);
    }

    public Task<ExportTemplateDownload?> DownloadTemplateAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        LastDownloadedFileId = fileId;
        return Task.FromResult(Download);
    }

    public Task<bool> SelectTemplateAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        LastSelectedFileId = fileId;
        return Task.FromResult(SelectResult);
    }

    public Task<bool> DeleteTemplateAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        LastDeletedFileId = fileId;
        return Task.FromResult(DeleteResult);
    }
}

public sealed class FakeBulkExportApiClient : IBulkExportApiClient
{
    public IReadOnlyList<BulkContractResponse> Contracts { get; set; } = [];

    public IReadOnlyList<SampleAddressResponse> SampleAddresses { get; set; } = [];

    public IReadOnlyList<ContractRenewalResponse> Renewals { get; set; } = [];

    public bool? LastNonUk { get; private set; }

    public Task<IReadOnlyList<BulkContractResponse>> GetContractsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Contracts);

    public Task<IReadOnlyList<SampleAddressResponse>> GetSampleAddressesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SampleAddresses);

    public Task<IReadOnlyList<ContractRenewalResponse>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default)
    {
        LastNonUk = nonUk;
        return Task.FromResult(Renewals);
    }
}
