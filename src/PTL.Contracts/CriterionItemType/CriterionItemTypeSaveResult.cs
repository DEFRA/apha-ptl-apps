namespace PTL.Contracts.CriterionItemType;

// Returned by PTL.ApiClient's CreateAsync/UpdateAsync/SetNoLongerInUseAsync so callers can surface
// validation errors (HTTP 400) or an in-use conflict (HTTP 409) without needing to catch an
// HttpRequestException - mirrors CategoryItemTypeSaveResult/TestTypeSaveResult.
public sealed record CriterionItemTypeSaveResult(bool Success, CriterionItemTypeResponse? CriterionItemType, IReadOnlyDictionary<string, string[]> FieldErrors);
