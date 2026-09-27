namespace PTL.Contracts.Contract;

public sealed record ContractDocumentRequest(
    string DocumentType,
    string TemplateName,
    string FileName,
    IReadOnlyDictionary<string, string> MergeValues,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? Regions = null,
    // Letters 2..n when one export produces several documents concatenated into one file.
    IReadOnlyList<ContractDocumentMergeData>? AdditionalDocuments = null);
