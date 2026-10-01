namespace PTL.Contracts.Contract;

// PUT /api/contracts/import-permits/{participantSchemeId} - matches legacy
// ImportPermitDataAccess.UpdateImportPermit; ImportPermitRequired is deliberately not included
// here - it is read-only, never written by this feature (see ImportPermitResponse).
public sealed record UpdateImportPermitRequest(bool ImportPermitReceived, DateTime? ImportPermitExpiry);
