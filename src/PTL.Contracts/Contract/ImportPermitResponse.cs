namespace PTL.Contracts.Contract;

// GET /api/contracts/{contractId}/import-permits - matches legacy PtaBusinessObjects.
// BusinessObjects.Contracts.ImportPermitInfo (see ImportPermit.aspx).
public sealed record ImportPermitResponse(
    Guid ParticipantSchemeId,
    string SchemeNumber,
    string SchemeName,
    string LabId,
    bool ImportPermitRequired,
    bool ImportPermitReceived,
    DateTime? ImportPermitExpiry);
