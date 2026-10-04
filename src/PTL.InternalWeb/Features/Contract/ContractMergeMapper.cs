using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Maps a contract to the Contract template's merge fields. Field names are taken verbatim from
/// legacy <c>MailMergeContract.DoMailMerge</c> and <c>GetContractItemsDataTable</c>.
/// </summary>
public static class ContractMergeMapper
{
    public static ContractDocumentRequest Build(ContractDocumentContext context)
    {
        var contract = context.Contract;
        var items = context.Items;
        var customer = context.Customer;
        var symbol = items?.Symbol ?? string.Empty;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContractNumber"] = contract.QalNumber,
            ["Suffix"] = contract.Suffix,
            ["PurchaseOrderNumber"] = contract.PurchaseOrderNumber,
            ["AccountNumber"] = customer?.AccountNumber ?? string.Empty,
            ["VatNumber"] = customer?.VatNumber ?? string.Empty,
            ["VatRating"] = MergeValueFormatting.VatRating(context.VatRatings, customer?.VatRatingId),

            ["ContactName"] = customer?.ContactName ?? string.Empty,
            ["CustomerOrganisation"] = customer?.Organisation ?? string.Empty,
            ["AddressLine1"] = customer?.Address1 ?? string.Empty,
            ["AddressLine2"] = customer?.Address2 ?? string.Empty,
            ["AddressLine3"] = customer?.Address3 ?? string.Empty,
            ["AddressLine4"] = customer?.Address4 ?? string.Empty,
            ["AddressLine5"] = customer?.Address5 ?? string.Empty,
            ["Country"] = MergeValueFormatting.Country(context.Countries, customer?.CountryId),
            ["Telephone"] = customer?.Telephone ?? string.Empty,
            ["Fax"] = customer?.Fax ?? string.Empty,
            ["Email"] = customer?.Email ?? string.Empty,
        };

        MergeValueFormatting.AddInvoiceFields(values, customer, context.Countries);
        MergeValueFormatting.AddFinancialFields(values, contract, items, symbol);

        var regions = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase)
        {
            [ContractDocumentTypes.ContractItemsRegion] = ContractItemRows(items, symbol),
        };

        return new ContractDocumentRequest(
            context.CanonicalDocumentType,
            context.TemplateKey,
            MergeValueFormatting.FileName(context.CanonicalDocumentType, contract.QalNumber, contract.Suffix),
            values,
            regions);
    }

    internal static List<IReadOnlyDictionary<string, string>> ContractItemRows(ContractItemsResponse? items, string symbol) =>
        items is null
            ? []
            : items.Schemes
                .SelectMany(scheme => scheme.Participants.Select(participant =>
                    (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["SchemeName"] = scheme.SchemeName,
                        ["SchemeIdentifier"] = scheme.SchemeIdentifier,
                        ["ParticipantName"] = participant.FullName,
                        ["LabName"] = participant.LabName,
                        ["LabCode"] = participant.LabCode,
                        ["NumberOfDistributions"] = MergeValueFormatting.Number(participant.NumberOfDistributions),
                        ["Price"] = MergeValueFormatting.Money(symbol, participant.Price),
                    }))
                .ToList();
}
