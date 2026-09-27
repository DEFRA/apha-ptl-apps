namespace PTL.Contracts.Contract;

public sealed record ContractDocumentResponse(
    string FileName,
    string ContentType,
    byte[] DocumentBytes);
