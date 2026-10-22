namespace PTL.Contracts.TestMethodItemType;

// Returned by PTL.ApiClient's CreateAsync/UpdateAsync/SetNoLongerInUseAsync so callers can surface
// validation errors (HTTP 400) or an in-use conflict (HTTP 409) without needing to catch an
// HttpRequestException - mirrors TestResultItemTypeSaveResult.
public sealed record TestMethodItemTypeSaveResult(bool Success, TestMethodItemTypeResponse? TestMethodItemType, IReadOnlyDictionary<string, string[]> FieldErrors);
