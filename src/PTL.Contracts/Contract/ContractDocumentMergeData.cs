namespace PTL.Contracts.Contract;

/// <summary>
/// One merged document's data. Used when a single export produces several letters that are
/// concatenated into one file, as legacy Address Confirmation does via Aspose <c>AppendChild</c>.
/// </summary>
public sealed record ContractDocumentMergeData(
    IReadOnlyDictionary<string, string> MergeValues,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? Regions = null);
