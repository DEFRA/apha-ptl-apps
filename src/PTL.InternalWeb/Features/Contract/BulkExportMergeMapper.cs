using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Builds one merge data set per record for the four bulk exports, reproducing legacy
/// <c>MailMergeContract.ExecuteAllContracts</c>, <c>MailMergeJobSheet.ExecuteAllJobSheets</c>,
/// <c>MailMergeRenewalLetter.ExecuteAllRenewalLetters</c> and
/// <c>MailMergeSampleAddressLetter.ExecuteAllSampleAddressLetters</c>. Each appends every record
/// into a single document.
/// </summary>
public static class BulkExportMergeMapper
{
    public static List<ContractDocumentMergeData> Contracts(IReadOnlyList<BulkContractResponse> contracts) =>
        [.. contracts.Select(BuildContract)];

    public static List<ContractDocumentMergeData> AddressConfirmationLetters(IReadOnlyList<SampleAddressResponse> addresses) =>
        [.. addresses.Select(AddressConfirmationMergeMapper.BuildLetter)];

    public static List<ContractDocumentMergeData> RenewalLetters(IReadOnlyList<ContractRenewalResponse> renewals) =>
        [.. renewals.Select(r => new ContractDocumentMergeData(RenewalLetterMergeMapper.BuildValues(r)))];

    // Contracts and Job Sheets merge the same vocabulary; legacy uses the same Contract object for
    // both and differs only in the template.
    private static ContractDocumentMergeData BuildContract(BulkContractResponse contract)
    {
        var symbol = contract.Symbol;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContractNumber"] = contract.ContractNumber,
            ["UtilityNumber"] = contract.ContractNumber,
            ["Suffix"] = contract.Suffix,
            ["QalNumber"] = contract.QalNumber,
            ["PurchaseOrderNumber"] = contract.PurchaseOrderNumber,
            ["AccountNumber"] = contract.AccountNumber,
            ["VatNumber"] = contract.VatNumber,
            ["VatRating"] = contract.VatRating,
            ["ContactName"] = contract.ContactName,
            ["CustomerContactName"] = contract.ContactName,
            ["CustomerOrganisation"] = contract.Organisation,
            ["Organisation"] = contract.Organisation,
            ["AddressLine1"] = contract.Address1,
            ["AddressLine2"] = contract.Address2,
            ["AddressLine3"] = contract.Address3,
            ["AddressLine4"] = contract.Address4,
            ["AddressLine5"] = contract.Address5,
            ["Country"] = contract.Country,
            ["Telephone"] = contract.Telephone,
            ["Fax"] = contract.Fax,
            ["Email"] = contract.Email,
            ["InvoiceName"] = contract.InvoiceName,
            ["InvoiceOrganisation"] = contract.InvoiceOrganisation,
            ["InvoiceAddressLine1"] = contract.InvoiceAddress1,
            ["InvoiceAddressLine2"] = contract.InvoiceAddress2,
            ["InvoiceAddressLine3"] = contract.InvoiceAddress3,
            ["InvoiceAddressLine4"] = contract.InvoiceAddress4,
            ["InvoiceAddressLine5"] = contract.InvoiceAddress5,
            ["InvoiceCountry"] = contract.InvoiceCountry,
            ["InvoiceTelephone"] = contract.InvoiceTelephone,
            ["InvoiceFax"] = contract.InvoiceFax,
            ["InvoiceEmail"] = contract.InvoiceEmail,
            ["AdminCharge"] = MergeValueFormatting.Money(symbol, contract.AdministrationCharge),
            ["PostageNumber"] = MergeValueFormatting.Number(contract.NumberPostage),
            ["CourierNumber"] = MergeValueFormatting.Number(contract.NumberCourier),
            ["SpecialDeliveryNumber"] = MergeValueFormatting.Number(contract.NumberSpecialDelivery),
            ["PostageCharge"] = MergeValueFormatting.Money(symbol, contract.PostagePrice),
            ["CourierCharge"] = MergeValueFormatting.Money(symbol, contract.CourierPrice),
            ["SpecialDeliveryCharge"] = MergeValueFormatting.Money(symbol, contract.SpecialDeliveryPrice),
            ["DiscountRate"] = MergeValueFormatting.Percentage(contract.DiscountRate),
            ["Discount"] = MergeValueFormatting.Money(symbol, contract.DiscountPrice),
            ["ContractTotal"] = MergeValueFormatting.Money(symbol, contract.TotalPrice),
            ["CommencementDate"] = MergeValueFormatting.Date(contract.CommencementDate),
            ["CompletionDate"] = MergeValueFormatting.CompletionDate(contract.CommencementDate),
        };

        var regions = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase)
        {
            [ContractDocumentTypes.ContractItemsRegion] = [.. contract.Items.Select(item =>
                (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SchemeName"] = item.SchemeName,
                    ["SchemeIdentifier"] = item.Identifier,
                    // Legacy Job Sheet and Contract items show LabName first, then LabCode.
                    ["ParticipantName"] = $"{item.LabName}: {item.LabCode}",
                    ["LabName"] = item.LabName,
                    ["LabCode"] = item.LabCode,
                    ["NumberOfDistributions"] = MergeValueFormatting.Number(item.NoOfDistributions),
                    ["Price"] = MergeValueFormatting.Money(symbol, item.Price),
                })],
        };

        return new ContractDocumentMergeData(values, regions);
    }
}
