using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Routes a document export to the mapper for that document type. Each legacy document has its own
/// merge field vocabulary and its own data source, so a single shared mapping cannot serve them all.
/// </summary>
public static class ContractDocumentMergeMapper
{
    public static ContractDocumentRequest Build(ContractDocumentContext context) => context.CanonicalDocumentType switch
    {
        ContractDocumentTypes.Contract => ContractMergeMapper.Build(context),
        ContractDocumentTypes.AddressConfirmation => AddressConfirmationMergeMapper.Build(context),
        ContractDocumentTypes.JobSheet => JobSheetMergeMapper.Build(context),
        ContractDocumentTypes.RenewalLetter => RenewalLetterMergeMapper.Build(context),
        _ => throw new ArgumentOutOfRangeException(nameof(context), context.CanonicalDocumentType, "Unsupported contract document type."),
    };
}
