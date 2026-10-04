namespace PTL.Core.Contract.Export.Templates;

public interface IUploadedTemplateRepository
{
    // spgaUploadedFiles - every template, every document type.
    Task<IReadOnlyList<UploadedTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    // spgUploadedFilesByDocumentType. Note the procedure does not return fldDocumentType.
    Task<IReadOnlyList<UploadedTemplate>> GetByDocumentTypeAsync(string documentType, CancellationToken cancellationToken = default);

    // spiUploadedFile
    Task CreateAsync(UploadedTemplate uploadedTemplate, CancellationToken cancellationToken = default);

    // spuUploadedFile - only the selected flag is updatable.
    Task SetSelectedAsync(Guid fileId, bool selected, CancellationToken cancellationToken = default);

    // spdUploadedFile
    Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);
}
