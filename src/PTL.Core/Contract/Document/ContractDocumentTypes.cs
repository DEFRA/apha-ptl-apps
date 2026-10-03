namespace PTL.Core.Contract.Document;

/// <summary>
/// Canonical names for the four per-contract document types and the merge-region names their
/// templates use. Which template file is merged is never decided here - that comes from the
/// selected row in <c>tblUploadedTemplate</c>, exactly as in legacy.
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

    private static readonly Dictionary<string, string> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["contract"] = Contract,
            ["contracts"] = Contract,
            ["addressconfirmation"] = AddressConfirmation,
            ["addressconfirmationletters"] = AddressConfirmation,
            ["jobsheet"] = JobSheet,
            ["jobsheets"] = JobSheet,
            ["renewalletter"] = RenewalLetter,
            ["renewalletters"] = RenewalLetter,
            ["contractrenewal"] = RenewalLetter
        };

    public static bool TryResolve(string? documentType, out string canonicalName)
    {
        canonicalName = string.Empty;

        if (string.IsNullOrWhiteSpace(documentType))
        {
            return false;
        }

        if (!Map.TryGetValue(Normalise(documentType), out var canonical))
        {
            return false;
        }

        canonicalName = canonical;
        return true;
    }

    // "Address Confirmation", "AddressConfirmation" and "address-confirmation" are all the same type.
    private static string Normalise(string documentType) =>
        new(documentType.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
