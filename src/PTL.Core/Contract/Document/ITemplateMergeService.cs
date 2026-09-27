using PTL.Contracts.Contract;

namespace PTL.Core.Contract.Document;

public interface ITemplateMergeService
{
    /// <param name="regions">
    /// Repeating table blocks keyed by region name, matching the legacy Aspose
    /// <c>ExecuteWithRegions</c> call (the Contract template's <c>ContractItems</c> region).
    /// </param>
    byte[] MergeTemplate(
        string templatePath,
        IReadOnlyDictionary<string, string> mergeValues,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? regions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges the template once per supplied data set and concatenates the results into a single
    /// document, reproducing legacy Aspose <c>AppendChild</c> (Address Confirmation emits one letter
    /// per sample address).
    /// </summary>
    byte[] MergeTemplateMany(
        string templatePath,
        IReadOnlyList<ContractDocumentMergeData> documents,
        CancellationToken cancellationToken = default);
}
