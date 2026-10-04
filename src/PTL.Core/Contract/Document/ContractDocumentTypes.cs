namespace PTL.Core.Contract.Document;

/// <summary>
/// Single source of truth mapping a legacy <c>UploadedTemplateCollection.DocumentType</c> to the
/// converted <c>.docx</c> template asset committed under <c>Documents/Templates</c>.
/// </summary>
public static class ContractDocumentTypes
{
    public const string Contract = "Contract";
    public const string AddressConfirmation = "Address Confirmation";
    public const string JobSheet = "Job Sheet";
    public const string RenewalLetter = "Renewal Letter";

    /// <summary>Region name of the repeating contract-item table block in the Contract template.</summary>
    public const string ContractItemsRegion = "ContractItems";

    /// <summary>Repeating scheme table blocks in the Address Confirmation template.</summary>
    public const string FeePayingSchemesRegion = "FeePayingSchemes";

    public const string NonFeePayingSchemesRegion = "NonFeePayingSchemes";

    private static readonly Dictionary<string, (string Canonical, string TemplateKey)> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["contract"] = (Contract, "ContractExampleTemplate"),
            ["contracts"] = (Contract, "ContractExampleTemplate"),
            ["addressconfirmation"] = (AddressConfirmation, "AddressConfirmationExampleTemplate"),
            ["addressconfirmationletters"] = (AddressConfirmation, "AddressConfirmationExampleTemplate"),
            ["jobsheet"] = (JobSheet, "JobSheetExampleTemplate"),
            ["jobsheets"] = (JobSheet, "JobSheetExampleTemplate"),
            ["renewalletter"] = (RenewalLetter, "ContractRenewalExampleTemplate"),
            ["renewalletters"] = (RenewalLetter, "ContractRenewalExampleTemplate"),
            ["contractrenewal"] = (RenewalLetter, "ContractRenewalExampleTemplate")
        };

    public static bool TryResolve(string? documentType, out string canonicalName, out string templateKey)
    {
        canonicalName = string.Empty;
        templateKey = string.Empty;

        if (string.IsNullOrWhiteSpace(documentType))
        {
            return false;
        }

        if (!Map.TryGetValue(Normalise(documentType), out var entry))
        {
            return false;
        }

        (canonicalName, templateKey) = entry;
        return true;
    }

    public static string? GetTemplateKey(string? documentType) =>
        TryResolve(documentType, out _, out var templateKey) ? templateKey : null;

    // "Address Confirmation", "AddressConfirmation" and "address-confirmation" are all the same type.
    private static string Normalise(string documentType) =>
        new(documentType.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
