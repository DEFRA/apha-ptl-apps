using System.Globalization;
using PTL.Contracts.Contract;
using PTL.Contracts.Customer;
using PTL.Contracts.Lookup;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Formatting shared by every contract document mapper. Reproduces the legacy Aspose merge value
/// formatting exactly: money as currency symbol + invariant "N2", discount rate as a percentage,
/// dates as dd/MM/yyyy.
/// </summary>
internal static class MergeValueFormatting
{
    internal const string DateFormat = "dd/MM/yyyy";
    internal const string MoneyFormat = "N2";

    internal static string Money(string symbol, decimal? value) =>
        value is null ? string.Empty : symbol + value.Value.ToString(MoneyFormat, CultureInfo.InvariantCulture);

    internal static string Percentage(decimal rate) =>
        (rate * 100m).ToString(MoneyFormat, CultureInfo.InvariantCulture);

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Date(DateTime? value) =>
        value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    // Legacy ContractRenewal exposes the two contract dates via DateTime.ToLongDateString().
    internal static string LongDate(DateTime? value) => value?.ToLongDateString() ?? string.Empty;

    internal static string Country(IReadOnlyList<CountryResponse> countries, Guid? countryId) =>
        countryId is { } id ? countries.FirstOrDefault(c => c.CountryId == id)?.Country ?? string.Empty : string.Empty;

    internal static string VatRating(IReadOnlyList<VatRatingResponse> vatRatings, Guid? vatRatingId) =>
        vatRatingId is { } id ? vatRatings.FirstOrDefault(v => v.VatRatingId == id)?.VatRating ?? string.Empty : string.Empty;

    /// <summary>
    /// Legacy Exports.Contract.CompletionDate: the UK financial year end following the commencement
    /// date - 31 March of the same year when commencement falls before April, otherwise 31 March of
    /// the following year. Empty when there is no commencement date.
    /// </summary>
    internal static string CompletionDate(DateTime? commencementDate)
    {
        if (commencementDate is not { } commencement)
        {
            return string.Empty;
        }

        var year = commencement.Month < 4 ? commencement.Year : commencement.Year + 1;
        return new DateTime(year, 3, 31, 0, 0, 0, DateTimeKind.Utc).ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Legacy spgaExportContractDetails derives fldContractNumber (the Job Sheet's UtilityNumber)
    /// as the UT number, falling back to the FT number when the UT number is blank.
    /// </summary>
    internal static string ContractNumber(string? utNumber, string? ftNumber) =>
        string.IsNullOrEmpty(utNumber) ? ftNumber ?? string.Empty : utNumber;

    internal static string FileName(string canonicalDocumentType, string qalNumber, string suffix)
    {
        var reference = string.Concat($"{qalNumber}{suffix}".Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        var type = string.Concat(canonicalDocumentType.Where(char.IsLetterOrDigit));
        return string.IsNullOrEmpty(reference) ? $"{type}.docx" : $"{type}-{reference}.docx";
    }

    // Shared by Contract and Job Sheet: identical Invoice* merge fields in every legacy template.
    internal static void AddInvoiceFields(IDictionary<string, string> values, CustomerResponse? customer, IReadOnlyList<CountryResponse> countries)
    {
        values["InvoiceName"] = customer?.InvoiceName ?? string.Empty;
        values["InvoiceOrganisation"] = customer?.InvoiceOrganisation ?? string.Empty;
        values["InvoiceAddressLine1"] = customer?.InvoiceAddress1 ?? string.Empty;
        values["InvoiceAddressLine2"] = customer?.InvoiceAddress2 ?? string.Empty;
        values["InvoiceAddressLine3"] = customer?.InvoiceAddress3 ?? string.Empty;
        values["InvoiceAddressLine4"] = customer?.InvoiceAddress4 ?? string.Empty;
        values["InvoiceAddressLine5"] = customer?.InvoiceAddress5 ?? string.Empty;
        values["InvoiceCountry"] = Country(countries, customer?.InvoiceCountryId);
        values["InvoiceTelephone"] = customer?.InvoiceTelephone ?? string.Empty;
        values["InvoiceFax"] = customer?.InvoiceFax ?? string.Empty;
        values["InvoiceEmail"] = customer?.InvoiceEmail ?? string.Empty;
    }

    // Shared by Contract and Job Sheet: identical postage/courier/discount/total/date merge fields.
    internal static void AddFinancialFields(IDictionary<string, string> values, ContractResponse contract, ContractItemsResponse? items, string symbol)
    {
        values["AdminCharge"] = Money(symbol, items?.AdministrationCharge ?? contract.AdministrationCharge);
        values["PostageNumber"] = Number(items?.NumberPostage ?? contract.NumberPostage);
        values["CourierNumber"] = Number(items?.NumberCourier ?? contract.NumberCourier);
        values["SpecialDeliveryNumber"] = Number(items?.NumberSpecialDelivery ?? contract.NumberSpecialDelivery);
        values["PostageCharge"] = Money(symbol, items?.PostagePriceTotal);
        values["CourierCharge"] = Money(symbol, items?.CourierPriceTotal);
        values["SpecialDeliveryCharge"] = Money(symbol, items?.SpecialDeliveryPriceTotal);
        values["DiscountRate"] = Percentage(items?.DiscountRate ?? contract.DiscountRate);
        values["Discount"] = Money(symbol, items?.DiscountPrice);
        values["ContractTotal"] = Money(symbol, items?.TotalPrice);
        values["CommencementDate"] = Date(contract.CommencementDate);
        values["CompletionDate"] = CompletionDate(contract.CommencementDate);
    }
}
