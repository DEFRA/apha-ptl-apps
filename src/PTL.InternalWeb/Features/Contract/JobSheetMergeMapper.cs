using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Maps a contract to the Job Sheet template's merge fields, from legacy
/// <c>MailMergeJobSheet.DoMailMerge</c>. Legacy uses <c>Customer</c>-prefixed names; the deployed
/// sample template uses unprefixed ones, so both are emitted - an unmatched field is blanked, so a
/// superset is safe and works with either template.
/// </summary>
public static class JobSheetMergeMapper
{
    public static ContractDocumentRequest Build(ContractDocumentContext context)
    {
        var contract = context.Contract;
        var items = context.Items;
        var customer = context.Customer;
        var symbol = items?.Symbol ?? string.Empty;

        var contactName = customer?.ContactName ?? string.Empty;
        var organisation = customer?.Organisation ?? string.Empty;
        var country = MergeValueFormatting.Country(context.Countries, customer?.CountryId);
        var telephone = customer?.Telephone ?? string.Empty;
        var fax = customer?.Fax ?? string.Empty;
        var email = customer?.Email ?? string.Empty;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UtilityNumber"] = MergeValueFormatting.ContractNumber(contract.UTNumber, contract.FTNumber),
            ["QalNumber"] = contract.QalNumber,
            ["ContractNumber"] = MergeValueFormatting.ContractNumber(contract.UTNumber, contract.FTNumber),
            ["Suffix"] = contract.Suffix,
            ["PurchaseOrderNumber"] = contract.PurchaseOrderNumber,
            ["AccountNumber"] = customer?.AccountNumber ?? string.Empty,
            ["VatNumber"] = customer?.VatNumber ?? string.Empty,
            ["VatRating"] = MergeValueFormatting.VatRating(context.VatRatings, customer?.VatRatingId),

            ["CustomerContactName"] = contactName,
            ["CustomerOrganisation"] = organisation,
            ["CustomerAddressLine1"] = customer?.Address1 ?? string.Empty,
            ["CustomerAddressLine2"] = customer?.Address2 ?? string.Empty,
            ["CustomerAddressLine3"] = customer?.Address3 ?? string.Empty,
            ["CustomerAddressLine4"] = customer?.Address4 ?? string.Empty,
            ["CustomerAddressLine5"] = customer?.Address5 ?? string.Empty,
            ["CustomerCountry"] = country,
            ["CustomerTelephone"] = telephone,
            ["CustomerFax"] = fax,
            ["CustomerEmail"] = email,

            // Unprefixed aliases used by the deployed sample template.
            ["ContactName"] = contactName,
            ["Organisation"] = organisation,
            ["AddressLine1"] = customer?.Address1 ?? string.Empty,
            ["AddressLine2"] = customer?.Address2 ?? string.Empty,
            ["AddressLine3"] = customer?.Address3 ?? string.Empty,
            ["AddressLine4"] = customer?.Address4 ?? string.Empty,
            ["AddressLine5"] = customer?.Address5 ?? string.Empty,
            ["Country"] = country,
            ["Telephone"] = telephone,
            ["Fax"] = fax,
            ["Email"] = email,
        };

        MergeValueFormatting.AddInvoiceFields(values, customer, context.Countries);
        MergeValueFormatting.AddFinancialFields(values, contract, items, symbol);

        var regions = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase)
        {
            [ContractDocumentTypes.ContractItemsRegion] = ContractMergeMapper.ContractItemRows(items, symbol),
        };

        return new ContractDocumentRequest(
            context.CanonicalDocumentType,
            context.TemplateName,
            MergeValueFormatting.FileName(context.CanonicalDocumentType, contract.QalNumber, contract.Suffix),
            values,
            regions);
    }
}
