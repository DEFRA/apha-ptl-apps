namespace PTL.Contracts.Scheme;

// Returned by PTL.ApiClient's CreateSchemeAsync/UpdateSchemeAsync so callers can surface
// validation errors (HTTP 400) without needing to catch an HttpRequestException. FieldErrors is
// keyed by the SchemeFormViewModel property name (empty key "" for a non-field-specific error),
// matching ASP.NET Core ModelState conventions - see ContractSaveResult for the reference pattern.
public sealed record SchemeSaveResult(bool Success, SchemeResponse? Scheme, IReadOnlyDictionary<string, string[]> FieldErrors);
