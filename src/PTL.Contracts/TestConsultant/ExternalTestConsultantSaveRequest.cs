namespace PTL.Contracts.TestConsultant;

// Posted to create (POST /api/external-test-consultants) or edit
// (PUT /api/external-test-consultants/{externalTestConsultantId}) a consultant - matches legacy
// ButtonAdd_Click / GridViewTC_Updating (Name, Department, Email only - Status/InactiveDate are
// changed via the separate status endpoint).
public sealed record ExternalTestConsultantSaveRequest(string Name, string Department, string Email);
