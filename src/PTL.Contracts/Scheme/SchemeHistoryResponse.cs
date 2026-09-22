namespace PTL.Contracts.Scheme;

// Public API contract for GET /api/schemes/families/{sharedId}/history - the projection returned
// by spgSchemeInfoBySharedId (SchemeHistory.aspx), ordered newest year first per the stored
// procedure's own ORDER BY.
public sealed record SchemeHistoryResponse(
    Guid SchemeId,
    Guid SharedId,
    int YearId,
    string Identifier,
    string Name);
