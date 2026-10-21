namespace PTL.Contracts.TestType;

// Returned by PTL.ApiClient's CreateAsync/UpdateAsync/SetNoLongerInUseAsync so callers can surface
// validation errors (HTTP 400) or an in-use conflict (HTTP 409) without needing to catch an
// HttpRequestException - mirrors CategoryItemTypeSaveResult.
public sealed record TestTypeSaveResult(bool Success, TestTypeResponse? TestType, IReadOnlyDictionary<string, string[]> FieldErrors);
