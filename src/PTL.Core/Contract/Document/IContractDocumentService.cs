namespace PTL.Core.Contract.Document;

public interface IContractDocumentService
{
    Task<PTL.Contracts.Contract.ContractDocumentResponse> GenerateAsync(
        PTL.Contracts.Contract.ContractDocumentRequest request,
        CancellationToken cancellationToken = default);
}
