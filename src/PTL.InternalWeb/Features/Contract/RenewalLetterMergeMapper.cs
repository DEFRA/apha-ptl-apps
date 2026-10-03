using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Maps a contract renewal to the Renewal Letter template's merge fields, from legacy
/// <c>MailMergeRenewalLetter.DoMailMerge</c>. The two contract dates use long date format because
/// legacy exposes them via <c>DateTime.ToLongDateString()</c>.
/// </summary>
public static class RenewalLetterMergeMapper
{
    public static ContractDocumentRequest Build(ContractDocumentContext context)
    {
        var values = BuildValues(context.Renewal);

        return new ContractDocumentRequest(
            context.CanonicalDocumentType,
            context.TemplateName,
            MergeValueFormatting.FileName(context.CanonicalDocumentType, context.Contract.QalNumber, context.Contract.Suffix),
            values);
    }

    public static Dictionary<string, string> BuildValues(ContractRenewalResponse? renewal) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["QalNumber"] = renewal?.QalNumber ?? string.Empty,
            ["OrganisationName"] = renewal?.OrganisationName ?? string.Empty,
            ["ContactName"] = renewal?.ContactName ?? string.Empty,
            ["AddressLine1"] = renewal?.Address1 ?? string.Empty,
            ["AddressLine2"] = renewal?.Address2 ?? string.Empty,
            ["AddressLine3"] = renewal?.Address3 ?? string.Empty,
            ["AddressLine4"] = renewal?.Address4 ?? string.Empty,
            ["AddressLine5"] = renewal?.Address5 ?? string.Empty,
            ["Country"] = renewal?.Country ?? string.Empty,
            ["CurrentContractStartDate"] = MergeValueFormatting.LongDate(renewal?.ContractStartDate),
            ["CurrentContractEndDate"] = MergeValueFormatting.LongDate(renewal?.ContractEndDate),
            ["AdditionalLetterInfo"] = renewal?.RenewalInformation ?? string.Empty,
        };
}
