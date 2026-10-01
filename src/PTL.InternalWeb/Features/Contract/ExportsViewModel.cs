using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>Links on the Exports parent page (legacy Contracts Admin/ExportMenu.aspx).</summary>
public sealed record ExportsMenuViewModel(IReadOnlyList<ExportsMenuItem> Items);

public sealed record ExportsMenuItem(string Text, string ActionName);

/// <summary>
/// One of the four export screens (legacy ExportContracts.aspx and siblings, all sharing
/// ExportBase). Every screen has the same layout; only the heading, the action names and the
/// export buttons differ.
/// </summary>
public sealed record ExportTemplatesViewModel(
    string DocumentType,
    string Title,
    string ActionName,
    IReadOnlyList<ExportTemplateResponse> Templates,
    bool HasSelectedTemplate)
{
    /// <summary>Renewal Letters is the only screen with separate UK and Non-UK export buttons.</summary>
    public bool IsRenewalLetters => DocumentType == Core.Contract.Export.Templates.ExportDocumentTypes.RenewalLetters;

    public string Instructions => Templates.Count == 0
        ? "No Mail Merge Templates have been uploaded."
        : "The selected Template that will be used during the Mail Merge is highlighted in the table below.";

    /// <summary>Legacy disabled the export button and showed this as its tooltip.</summary>
    public const string ExportDisabledTooltip = "Please select a Template for use during the Export.";

    public const string UploadInstructions = "To use a new Template, browse for it and then upload it.";

    public const string DeleteConfirmation = "Are you sure that you want to delete this template?";
}
