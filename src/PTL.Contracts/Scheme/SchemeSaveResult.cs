namespace PTL.Contracts.Scheme;

// Returned by PTL.ApiClient's CreateSchemeAsync/UpdateSchemeAsync so callers can surface
// validation errors (HTTP 400) without needing to catch an HttpRequestException. FieldErrors is
// keyed by the SchemeFormViewModel property name (empty key "" for a non-field-specific error),
// matching ASP.NET Core ModelState conventions - see ContractSaveResult for the reference pattern.
// When AuditReasonRequired is true nothing was saved: the edit changes an audited Test or
// Tabulation, so resubmit with SchemeRequest.AuditWhy.
public sealed record SchemeSaveResult(
    bool Success,
    SchemeResponse? Scheme,
    IReadOnlyDictionary<string, string[]> FieldErrors,
    bool AuditReasonRequired = false,
    string AuditObjectName = "");
