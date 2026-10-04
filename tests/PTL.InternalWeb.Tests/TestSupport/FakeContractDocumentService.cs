using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.InternalWeb.Tests.TestSupport;

public sealed class FakeContractDocumentService : IContractDocumentService
{
    public ContractDocumentResponse Response { get; set; } =
        new("contract.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", []);

    public Exception? ExceptionToThrow { get; set; }

    public ContractDocumentRequest? LastRequest { get; private set; }

    public Task<ContractDocumentResponse> GenerateAsync(ContractDocumentRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return ExceptionToThrow is null ? Task.FromResult(Response) : throw ExceptionToThrow;
    }
}
