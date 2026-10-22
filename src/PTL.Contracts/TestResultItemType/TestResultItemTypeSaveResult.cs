namespace PTL.Contracts.TestResultItemType;

// Returned by PTL.ApiClient's CreateAsync/UpdateAsync/SetNoLongerInUseAsync so callers can surface
// validation errors (HTTP 400) or an in-use conflict (HTTP 409) without needing to catch an
// HttpRequestException - mirrors TestTypeSaveResult.
public sealed record TestResultItemTypeSaveResult(bool Success, TestResultItemTypeResponse? TestResultItemType, IReadOnlyDictionary<string, string[]> FieldErrors);
