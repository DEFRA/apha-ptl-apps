namespace PTL.Core.Contract.ImportPermit;

// Matches legacy PtaBusinessObjects.BusinessObjects.Contracts.ImportPermitInfo - one row per
// participant-scheme on a contract. ImportPermitRequired is sourced from
// tlnkParticipantScheme.fldImportExportLicenceRequired (the same flag already modelled as
// ParticipantSchemeRecord.ImportExportLicenceRequired) - it is never itself written by this
// feature, matching legacy's permanently-disabled chkRequired checkbox.
public class ImportPermitEntity
{
    public Guid ParticipantSchemeId { get; set; }

    public string SchemeNumber { get; set; } = string.Empty;

    public string SchemeName { get; set; } = string.Empty;

    public string LabId { get; set; } = string.Empty;

    public bool ImportPermitRequired { get; set; }

    public bool ImportPermitReceived { get; set; }

    public DateTime? ImportPermitExpiry { get; set; }
}
