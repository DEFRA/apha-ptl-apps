namespace PTL.InternalWeb.Features.Contract;

// Legacy ImportPermit.aspx (GridViewImportPermits) - see PtaBusinessObjects.BusinessObjects.
// Contracts.ImportPermitInfo. EditParticipantSchemeId replaces WebForms' GridView.EditIndex
// ViewState - only that one row (if any) renders its Update/Cancel edit-mode controls; every other
// row renders read-only with an Edit action, matching legacy's CommandField ShowEditButton exactly.
public sealed class ImportPermitsViewModel
{
    public Guid ContractId { get; set; }

    public List<ImportPermitRowViewModel> Permits { get; set; } = [];

    public Guid? EditParticipantSchemeId { get; set; }
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
