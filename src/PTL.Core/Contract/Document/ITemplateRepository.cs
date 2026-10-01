namespace PTL.Core.Contract.Document;

public interface ITemplateRepository
{
    Task<DocumentTemplate?> GetAsync(
        string documentType,
        string? templateName = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTemplate>> GetByDocumentTypeAsync(
        string documentType,
        CancellationToken cancellationToken = default);
}
