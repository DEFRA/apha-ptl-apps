namespace PTL.InternalWeb.Features.Contract;

// Legacy ImportPermit.aspx (GridViewImportPermits) - see PtaBusinessObjects.BusinessObjects.
// Contracts.ImportPermitInfo. EditParticipantSchemeId replaces WebForms' GridView.EditIndex
// ViewState - only that one row (if any) renders its Update/Cancel edit-mode controls; every other
// row renders read-only with an Edit action, matching legacy's CommandField ShowEditButton exactly.
//
// Legacy kept the row edits in an in-memory list (ViewState) until Apply/Save wrote them to the
// database. This app has no server session, so the rows' pending Received/Expiry values are
// round-tripped through the page's form (see ImportPermitPendingRow) instead.
public sealed class ImportPermitsViewModel
{
    public Guid ContractId { get; set; }

    public List<ImportPermitRowViewModel> Permits { get; set; } = [];

    public Guid? EditParticipantSchemeId { get; set; }

    // Values typed into the row being edited, redisplayed when its Update fails validation.
    public bool? EditReceived { get; set; }

    public string? EditExpiry { get; set; }
}

// Submit-button commands understood by the ContractController.ImportPermits POST.
public static class ImportPermitCommands
{
    public const string Edit = "edit";

    public const string Update = "update";

    public const string CancelEdit = "cancelEdit";

    public const string Apply = "apply";

    public const string Save = "save";

    public const string Cancel = "cancel";
}

// One row's pending (not yet saved) Received/Expiry, posted back by the page's form.
public sealed class ImportPermitPendingRow
{
    public required Guid ParticipantSchemeId { get; set; }

    public bool? ImportPermitReceived { get; set; }

    public string? ImportPermitExpiry { get; set; }
}

public sealed class ImportPermitRowViewModel
{
    public Guid ParticipantSchemeId { get; set; }

    public string SchemeNumber { get; set; } = string.Empty;

    public string SchemeName { get; set; } = string.Empty;

    public string LabId { get; set; } = string.Empty;

    // Read-only (sourced from ParticipantScheme.ImportExportLicenceRequired) - never posted back
    // as an editable field, matching legacy's permanently-disabled chkRequired checkbox.
    public bool ImportPermitRequired { get; set; }

    public bool ImportPermitReceived { get; set; }

    // Text input matching legacy's dd/MM/yyyy SmartDate display/entry format exactly.
    public string? ImportPermitExpiry { get; set; }
}
