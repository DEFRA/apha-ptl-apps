namespace PTL.Contracts.Scheme;

// Body of the 428 returned by PUT /api/schemes/{id} when the edit changes an audited Test or
// Tabulation and no AuditWhy was supplied (legacy diverted that save through Audit.aspx).
// AuditObjectName is legacy's "Scheme {identifier}: {name}" heading for that page.
public sealed record SchemeAuditRequiredResponse(string AuditObjectName);
