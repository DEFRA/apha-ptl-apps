using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Maps each sample address on a contract to one Address Confirmation letter, from legacy
/// <c>MailMergeSampleAddressLetter.DoMailMerge</c>. Legacy appends one letter per sample address
/// into a single document, which is reproduced via <c>AdditionalDocuments</c>.
/// </summary>
public static class AddressConfirmationMergeMapper
{
    public static ContractDocumentRequest Build(ContractDocumentContext context)
    {
        var letters = context.SampleAddresses.Select(BuildLetter).ToList();

        var primary = letters.Count > 0
            ? letters[0]
            : new ContractDocumentMergeData(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return new ContractDocumentRequest(
            context.CanonicalDocumentType,
            context.TemplateKey,
            MergeValueFormatting.FileName(context.CanonicalDocumentType, context.Contract.QalNumber, context.Contract.Suffix),
            primary.MergeValues,
            primary.Regions,
            letters.Skip(1).ToList());
    }

    private static ContractDocumentMergeData BuildLetter(SampleAddressResponse address)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["QalNumber"] = address.QalNumber,
            ["Labcode"] = address.LabCode,
            ["LabCode"] = address.LabCode,
            ["ContactName"] = address.ContactName,
            ["Organisation"] = address.Organisation,
            ["CustomerOrganisation"] = address.Organisation,
            ["AddressLine1"] = address.Address1,
            ["AddressLine2"] = address.Address2,
            ["AddressLine3"] = address.Address3,
            ["AddressLine4"] = address.Address4,
            ["AddressLine5"] = address.Address5,
            ["Country"] = address.Country,
            ["Telephone"] = address.Telephone,
            ["Fax"] = address.Fax,
            ["Email"] = address.Email,
            ["VatNumber"] = address.VatNumber,
            ["AccountNumber"] = address.AccountNumber,
            ["VatRating"] = address.VatRating,
            ["PurchaseOrderNumber"] = address.PurchaseOrderNumber,
        };

        var regions = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase)
        {
            [ContractDocumentTypes.FeePayingSchemesRegion] = SchemeRows(address.FeePayingSchemes),
            [ContractDocumentTypes.NonFeePayingSchemesRegion] = SchemeRows(address.NonFeePayingSchemes),
        };

        return new ContractDocumentMergeData(values, regions);
    }

    private static List<IReadOnlyDictionary<string, string>> SchemeRows(IReadOnlyList<SampleAddressSchemeResponse> schemes) =>
        schemes.Select(scheme =>
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SchemeName"] = scheme.SchemeName,
                ["SchemeIdentifier"] = scheme.SchemeIdentifier,
                ["MonthsActive"] = scheme.MonthsActive,
                ["WeekNumber"] = MergeValueFormatting.Number(scheme.WeekNumber),
            })
        .ToList();
}
