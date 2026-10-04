namespace PTL.Core.Contract.Export.Templates;

/// <summary>
/// The four export types shown under "Manage Contracts &gt; Exports". The storage names are the
/// legacy <c>UploadedTemplateCollection.DocumentType</c> enum values written to
/// <c>tblUploadedTemplate.fldDocumentType</c>, so they must not be renamed.
/// </summary>
public static class ExportDocumentTypes
{
    public const string Contracts = "Contracts";
    public const string JobSheets = "JobSheets";
    public const string RenewalLetters = "RenewalLetters";
    public const string AddressConfirmationLetters = "AddressConfirmationLetters";

    private static readonly Dictionary<string, (string StorageName, string DisplayName)> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["contract"] = (Contracts, "Export Contracts"),
            ["contracts"] = (Contracts, "Export Contracts"),
            ["jobsheet"] = (JobSheets, "Export Job Sheets"),
            ["jobsheets"] = (JobSheets, "Export Job Sheets"),
            ["renewalletter"] = (RenewalLetters, "Export Renewal Letters"),
            ["renewalletters"] = (RenewalLetters, "Export Renewal Letters"),
            ["addressconfirmation"] = (AddressConfirmationLetters, "Export Address Confirmation Letters"),
            ["addressconfirmationletters"] = (AddressConfirmationLetters, "Export Address Confirmation Letters")
        };

    public static IReadOnlyList<string> All { get; } =
        [Contracts, JobSheets, RenewalLetters, AddressConfirmationLetters];

    public static bool TryResolve(string? documentType, out string storageName, out string displayName)
    {
        storageName = string.Empty;
        displayName = string.Empty;

        if (string.IsNullOrWhiteSpace(documentType) || !Map.TryGetValue(Normalise(documentType), out var entry))
        {
            return false;
        }

        (storageName, displayName) = entry;
        return true;
    }

    public static string DisplayNameFor(string documentType) =>
        TryResolve(documentType, out _, out var displayName) ? displayName : documentType;

    private static string Normalise(string documentType) =>
        new(documentType.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
